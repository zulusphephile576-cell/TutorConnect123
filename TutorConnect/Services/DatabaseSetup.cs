using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace TutorConnect.Services
{
    /// <summary>
    /// First-run helper behind /Setup. It checks the connection and can install the database by running
    /// Database/01_Schema.sql, 02_StoredProcedures.sql and 03_SeedData.sql in order.
    /// This is the only code that runs SQL text directly; it's installation tooling, not application data
    /// access, which remains stored-procedure only.
    /// </summary>
    public static class DatabaseSetup
    {
        public class Status
        {
            public string Server { get; set; }
            public string Database { get; set; }
            public bool ServerReachable { get; set; }
            public string ServerError { get; set; }
            public bool DatabaseExists { get; set; }
            public bool TablesInstalled { get; set; }
            public bool ProceduresInstalled { get; set; }
            public bool ColumnsCurrent { get; set; }
            /// <summary>Installed, but from an older version: can be upgraded in place without losing data.</summary>
            public bool NeedsUpgrade => TablesInstalled && (!ColumnsCurrent || !ProceduresInstalled);
            public int UserCount { get; set; }
            public string ScriptsFolder { get; set; }
            public bool Ready => ServerReachable && DatabaseExists && TablesInstalled && ProceduresInstalled && ColumnsCurrent;
        }

        public static readonly string[] Scripts = { "01_Schema.sql", "02_StoredProcedures.sql", "03_SeedData.sql" };

        public static bool Enabled => AppConfig.GetBool("Setup:Enabled", true);

        private static SqlConnectionStringBuilder Builder()
        {
            return new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["TutorConnect"].ConnectionString);
        }

        public static string DatabaseName
        {
            get
            {
                var name = Builder().InitialCatalog;
                return string.IsNullOrWhiteSpace(name) ? "TutorConnect" : name;
            }
        }

        /// <summary>Looks for the Database folder next to the web project, then inside App_Data.</summary>
        public static string FindScriptsFolder()
        {
            var root = HostingEnvironment.MapPath("~/") ?? AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(root, "..", "Database")),
                Path.GetFullPath(Path.Combine(root, "..", "..", "Database")),
                Path.Combine(root, "App_Data", "Database")
            };
            foreach (var c in candidates)
                if (File.Exists(Path.Combine(c, Scripts[0]))) return c;
            return null;
        }

        public static Status Check()
        {
            var b = Builder();
            var s = new Status { Server = b.DataSource, Database = DatabaseName, ScriptsFolder = FindScriptsFolder() };

            // Hosted databases (Azure SQL, shared hosting) often don't allow access to master, so connect to the
            // database itself first; only fall back to master to find out whether it exists.
            try
            {
                using (var cn = new SqlConnection(new SqlConnectionStringBuilder(b.ConnectionString) { ConnectTimeout = 8 }.ConnectionString))
                {
                    cn.Open();
                    s.ServerReachable = true;
                    s.DatabaseExists = true;
                }
            }
            catch (SqlException direct)
            {
                var master = new SqlConnectionStringBuilder(b.ConnectionString) { InitialCatalog = "master", ConnectTimeout = 8 };
                try
                {
                    using (var cn = new SqlConnection(master.ConnectionString))
                    {
                        cn.Open();
                        s.ServerReachable = true;
                        using (var cmd = new SqlCommand("SELECT DB_ID(@n)", cn))
                        {
                            cmd.Parameters.AddWithValue("@n", s.Database);
                            s.DatabaseExists = cmd.ExecuteScalar() != DBNull.Value;
                        }
                    }
                    if (s.DatabaseExists) { s.ServerError = direct.GetBaseException().Message; return s; }
                }
                catch (Exception)
                {
                    s.ServerError = direct.GetBaseException().Message;
                    return s;
                }
            }
            if (!s.DatabaseExists) return s;

            try
            {
                using (var cn = new SqlConnection(new SqlConnectionStringBuilder(b.ConnectionString) { ConnectTimeout = 8 }.ConnectionString))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CASE WHEN OBJECT_ID('dbo.Users') IS NULL THEN 0 ELSE 1 END, " +
                        "(SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'sp[_]%'), " +
                        "CASE WHEN OBJECT_ID('dbo.sp_Notifications_Get') IS NULL OR OBJECT_ID('dbo.sp_User_UpdateProfile') IS NULL OR OBJECT_ID('dbo.sp_ApiToken_Validate') IS NULL OR OBJECT_ID('dbo.sp_Faculty_List') IS NULL OR OBJECT_ID('dbo.sp_Ai_LogRequest') IS NULL OR OBJECT_ID('dbo.sp_Admin_AnalyticsDetail') IS NULL OR OBJECT_ID('dbo.sp_Module_BulkCreate') IS NULL OR OBJECT_ID('dbo.sp_Payment_SetPayer') IS NULL OR OBJECT_ID('dbo.sp_Tutorial_Update') IS NULL OR OBJECT_ID('dbo.sp_Content_ForStudent') IS NULL OR OBJECT_ID('dbo.sp_Tutorials_AutoMarkAbsent') IS NULL THEN 0 ELSE 1 END, " +
                        "CASE WHEN COL_LENGTH('dbo.Users', 'EmergencyContactPhone') IS NULL OR COL_LENGTH('dbo.Users', 'LockoutUntil') IS NULL " +
                        "OR OBJECT_ID('dbo.ApiTokens') IS NULL OR OBJECT_ID('dbo.fn_SaNow') IS NULL OR OBJECT_ID('dbo.AiRequests') IS NULL " +
                        "OR COL_LENGTH('dbo.Courses', 'DepartmentId') IS NULL OR COL_LENGTH('dbo.Modules', 'TutorsNeeded') IS NULL OR COL_LENGTH('dbo.Tutorials', 'CheckInCode') IS NULL OR COL_LENGTH('dbo.Modules', 'Semester') IS NULL OR COL_LENGTH('dbo.Payments', 'PayerBank') IS NULL OR COL_LENGTH('dbo.TutorApplications', 'CvPath') IS NULL OR COL_LENGTH('dbo.ActivityLog', 'Link') IS NULL THEN 0 ELSE 1 END", cn))
                    using (var r = cmd.ExecuteReader())
                    {
                        r.Read();
                        s.TablesInstalled = r.GetInt32(0) == 1;
                        s.ProceduresInstalled = r.GetInt32(1) >= 80 && r.GetInt32(2) == 1;
                        s.ColumnsCurrent = r.GetInt32(3) == 1;
                    }
                    if (s.TablesInstalled)
                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users", cn))
                            s.UserCount = (int)cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                s.ServerError = ex.GetBaseException().Message;
            }
            return s;
        }

        /// <summary>Runs the scripts. Returns one line per script describing what happened.</summary>
        public static List<string> Install(bool includeDemoData)
        {
            var log = new List<string>();
            var folder = FindScriptsFolder();
            if (folder == null) throw new InvalidOperationException("The Database folder with the three .sql scripts couldn't be found next to the web project.");

            using (var cn = OpenTarget(createIfMissing: true))
            {
                foreach (var file in Scripts)
                {
                    if (file == "03_SeedData.sql" && !includeDemoData)
                    {
                        RunBatch(cn, SeedMinimalAdmin());
                        log.Add("Created the administrator account only (demo data skipped).");
                        continue;
                    }
                    log.Add(file + ": ran " + RunScript(cn, Path.Combine(folder, file)) + " batches.");
                }
            }
            return log;
        }

        /// <summary>
        /// Upgrades an existing database in place, keeping all data: adds new columns (04_Upgrade.sql),
        /// then re-creates the stored procedures (02_StoredProcedures.sql, which only uses CREATE OR ALTER).
        /// </summary>
        public static List<string> Upgrade()
        {
            var folder = FindScriptsFolder();
            if (folder == null) throw new InvalidOperationException("The Database folder with the .sql scripts couldn't be found next to the web project.");
            var log = new List<string>();
            using (var cn = OpenTarget(createIfMissing: false))
            {
                foreach (var file in new[] { "04_Upgrade.sql", "02_StoredProcedures.sql" })
                    log.Add(file + ": ran " + RunScript(cn, Path.Combine(folder, file)) + " batches.");
            }
            AppLog.Write("SETUP", "Database upgraded in place: " + string.Join(" ", log));
            return log;
        }

        /// <summary>Demo tutors' bank accounts (fake numbers with each bank's correct length).</summary>
        private static readonly string[][] DemoBanking =
        {
            new[] { "tutor@tutorconnect.ac.za",           "fnb",      "S Ndlovu",      "Cheque / current", "62845739201" },
            new[] { "zinhle.mkhize@tutorconnect.ac.za",   "capitec",  "Z Mkhize",      "Savings",          "1462738495" },
            new[] { "kabelo.molefe@tutorconnect.ac.za",   "standard", "K Molefe",      "Cheque / current", "10158936274" },
            new[] { "ruan.pretorius@tutorconnect.ac.za",  "absa",     "R Pretorius",   "Cheque / current", "4092817365" },
            new[] { "nadia.pillay@tutorconnect.ac.za",    "nedbank",  "N Pillay",      "Savings",          "1194827365" },
            new[] { "musa.zungu@tutorconnect.ac.za",      "tyme",     "M Zungu",       "Savings",          "51028374659" },
            new[] { "lerato.mahlangu@tutorconnect.ac.za", "capitec",  "L Mahlangu",    "Savings",          "1583920476" }
        };

        /// <summary>
        /// Runs Database/05_MoreDemoUsers.sql (adds people without deleting anything), then gives every demo tutor
        /// banking details so payments to them work straight away. Returns a summary line per step.
        /// </summary>
        public static List<string> AddDemoUsers()
        {
            var folder = FindScriptsFolder();
            if (folder == null || !File.Exists(Path.Combine(folder, "05_MoreDemoUsers.sql")))
                throw new InvalidOperationException("Database/05_MoreDemoUsers.sql couldn't be found next to the web project.");
            var log = new List<string>();
            using (var cn = OpenTarget(createIfMissing: false))
            {
                RunScript(cn, Path.Combine(folder, "05_MoreDemoUsers.sql"));
            }
            log.Add("Added the demo administrator, lecturers, tutors and students (anyone already there was skipped).");

            var banked = EnsureDemoBanking();
            log.Add(banked == 0 ? "Demo tutors already had banking details." : "Added banking details for " + banked + (banked == 1 ? " tutor" : " tutors") + ", so students can pay them.");
            AppLog.Write("SETUP", "Demo users added: " + string.Join(" ", log));
            return log;
        }

        /// <summary>
        /// Gives each demo tutor a usable bank account: adds the details if they have none, or encrypts the account
        /// number when the SQL scripts left it empty (SQL can't use the site's key). Real tutors' details are never
        /// changed: only the listed demo tutors are touched, and only when the last four digits match the demo account.
        /// Returns how many tutors were updated.
        /// </summary>
        public static int EnsureDemoBanking()
        {
            var updated = 0;
            foreach (var b in DemoBanking)
            {
                var tutor = TutorConnect.Data.Db.Single<TutorConnect.Models.User>("sp_User_GetByEmailRole",
                    TutorConnect.Data.Db.P("@Email", b[0]), TutorConnect.Data.Db.P("@Role", "Tutor"));
                if (tutor == null) continue;
                var existing = TutorConnect.Data.Db.Single<TutorConnect.Models.BankingDetails>("sp_Banking_Get", TutorConnect.Data.Db.P("@TutorId", tutor.UserId));
                var needsNumber = existing != null && string.IsNullOrEmpty(existing.AccountNumberProtected)
                                  && existing.AccountLast4 == SouthAfricanBanks.Last4(b[4]);
                if (existing != null && !needsNumber) continue;   // real or already complete: leave it alone
                var bank = SouthAfricanBanks.Find(b[1]);
                TutorConnect.Data.Db.Exec("sp_Banking_Save", TutorConnect.Data.Db.P("@TutorId", tutor.UserId), TutorConnect.Data.Db.P("@BankName", bank.Name),
                    TutorConnect.Data.Db.P("@AccountHolder", b[2]), TutorConnect.Data.Db.P("@AccountType", b[3]), TutorConnect.Data.Db.P("@BranchCode", bank.BranchCode),
                    TutorConnect.Data.Db.P("@AccountNumberProtected", SecretProtector.Protect(b[4])), TutorConnect.Data.Db.P("@AccountLast4", SouthAfricanBanks.Last4(b[4])));
                updated++;
            }
            if (updated > 0) AppLog.Write("SETUP", "Demo banking details completed for " + updated + " tutor(s).");
            return updated;
        }

        /// <summary>
        /// Opens a connection to the application's database. If it doesn't exist yet (and createIfMissing), it is
        /// created through master first, which works on SQL Server Express. Hosts that provide a ready-made
        /// database (Azure SQL, shared hosting) are connected to directly.
        /// </summary>
        private static SqlConnection OpenTarget(bool createIfMissing)
        {
            var target = new SqlConnectionStringBuilder(Builder().ConnectionString) { InitialCatalog = DatabaseName };
            try
            {
                var cn = new SqlConnection(target.ConnectionString);
                cn.Open();
                return cn;
            }
            catch (SqlException) when (createIfMissing)
            {
                var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
                using (var m = new SqlConnection(master.ConnectionString))
                {
                    m.Open();
                    using (var cmd = new SqlCommand("IF DB_ID(@n) IS NULL EXEC ('CREATE DATABASE ' + @q);", m))
                    {
                        cmd.Parameters.AddWithValue("@n", DatabaseName);
                        cmd.Parameters.AddWithValue("@q", "[" + DatabaseName.Replace("]", "]]") + "]");
                        cmd.ExecuteNonQuery();
                    }
                }
                SqlConnection.ClearAllPools();
                var cn = new SqlConnection(target.ConnectionString);
                cn.Open();
                return cn;
            }
        }

        private static int RunScript(SqlConnection cn, string path)
        {
            // The connection already points at the right database, so the scripts' USE and CREATE DATABASE lines
            // are dropped (Azure SQL doesn't allow USE at all).
            var sql = Regex.Replace(File.ReadAllText(path), @"^\s*IF DB_ID\('TutorConnect'\) IS NULL CREATE DATABASE TutorConnect;\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"^\s*USE\s+\[?TutorConnect\]?\s*;?\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            sql = RetargetDatabase(sql, DatabaseName);
            var batches = Regex.Split(sql, @"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var count = 0;
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                RunBatch(cn, batch);
                count++;
            }
            return count;
        }

        private static void RunBatch(SqlConnection cn, string sql)
        {
            using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 180 })
                cmd.ExecuteNonQuery();
        }

        /// <summary>The scripts target a database called TutorConnect; follow the connection string if it names another.</summary>
        private static string RetargetDatabase(string sql, string name)
        {
            if (name == "TutorConnect") return sql;
            var quoted = "[" + name.Replace("]", "]]") + "]";
            return sql.Replace("DB_ID('TutorConnect')", "DB_ID('" + name.Replace("'", "''") + "')")
                      .Replace("CREATE DATABASE TutorConnect", "CREATE DATABASE " + quoted)
                      .Replace("USE TutorConnect", "USE " + quoted);
        }

        private static string SeedMinimalAdmin()
        {
            return "IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Role = 'Admin') " +
                   "INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role) VALUES " +
                   "(N'System', N'Administrator', N'admin@tutorconnect.ac.za', N'" + PasswordHasher.Hash("Admin@123") + "', 'Admin');";
        }
    }
}
