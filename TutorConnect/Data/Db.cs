using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace TutorConnect.Data
{
    /// <summary>
    /// Thin ADO.NET helper. Every call is a stored procedure (no inline SQL, no ORM).
    /// Result rows are mapped onto POCOs by matching column names to property names.
    /// </summary>
    public static class Db
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

        private static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["TutorConnect"].ConnectionString;

        public static SqlParameter P(string name, object value) => new SqlParameter(name, value ?? DBNull.Value);

        public static SqlParameter Table(string name, string typeName, DataTable value) =>
            new SqlParameter(name, SqlDbType.Structured) { TypeName = typeName, Value = value };

        private static SqlCommand Command(SqlConnection connection, string procedure, SqlParameter[] parameters)
        {
            var cmd = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
            if (parameters != null) cmd.Parameters.AddRange(parameters);
            return cmd;
        }

        public static DataSet QuerySet(string procedure, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var cmd = Command(connection, procedure, parameters))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                var ds = new DataSet();
                adapter.Fill(ds);
                return ds;
            }
        }

        public static DataTable Query(string procedure, params SqlParameter[] parameters)
        {
            var ds = QuerySet(procedure, parameters);
            return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }

        public static List<T> List<T>(string procedure, params SqlParameter[] parameters) where T : new() =>
            Map<T>(Query(procedure, parameters));

        public static T Single<T>(string procedure, params SqlParameter[] parameters) where T : class, new() =>
            List<T>(procedure, parameters).FirstOrDefault();

        public static void Exec(string procedure, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var cmd = Command(connection, procedure, parameters))
            {
                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string procedure, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var cmd = Command(connection, procedure, parameters))
            {
                connection.Open();
                var result = cmd.ExecuteScalar();
                return result == DBNull.Value ? null : result;
            }
        }

        public static int ScalarInt(string procedure, params SqlParameter[] parameters)
        {
            var value = Scalar(procedure, parameters);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        public static List<T> Map<T>(DataTable table) where T : new()
        {
            var list = new List<T>();
            if (table == null) return list;
            var props = PropertyCache.GetOrAdd(typeof(T), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToArray());
            var columns = props.Where(p => table.Columns.Contains(p.Name)).ToArray();

            foreach (DataRow row in table.Rows)
            {
                var item = new T();
                foreach (var prop in columns)
                {
                    var value = row[prop.Name];
                    if (value == DBNull.Value) continue;
                    var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    prop.SetValue(item, target.IsInstanceOfType(value) ? value : Convert.ChangeType(value, target));
                }
                list.Add(item);
            }
            return list;
        }

        /// <summary>Turns the first row of a table into a case-insensitive dictionary (used for stat rows).</summary>
        public static Dictionary<string, object> Row(DataTable table)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (table == null || table.Rows.Count == 0) return dict;
            foreach (DataColumn col in table.Columns)
            {
                var v = table.Rows[0][col];
                dict[col.ColumnName] = v == DBNull.Value ? null : v;
            }
            return dict;
        }

        /// <summary>True when the exception came from a THROW 5xxxx in a stored procedure (a message meant for the user).</summary>
        public static bool IsUserError(Exception ex, out string message)
        {
            var sql = ex as SqlException;
            if (sql != null && sql.Number >= 50000)
            {
                message = sql.Message;
                return true;
            }
            message = null;
            return false;
        }
    }
}
