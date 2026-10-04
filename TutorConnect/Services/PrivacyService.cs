using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TutorConnect.Data;
using TutorConnect.Helpers;

namespace TutorConnect.Services
{
    /// <summary>POPIA section 23 (right of access): everything TutorConnect holds about one person.</summary>
    public static class PrivacyService
    {
        private static readonly string[] Sections =
            { "profile", "modules", "bookings", "payments", "attendance", "tutorApplications", "banking", "activity" };

        public static Dictionary<string, object> Export(int userId)
        {
            var ds = Db.QuerySet("sp_User_DataExport", Db.P("@UserId", userId));
            var result = new Dictionary<string, object>
            {
                { "exportedAt", Clock.Iso(Clock.Now) },
                { "timeZone", "South Africa Standard Time (UTC+02:00)" },
                { "currency", "ZAR" },
                { "notice", "This is all the personal information TutorConnect holds about you, provided under the Protection of Personal Information Act (POPIA)." }
            };
            for (var i = 0; i < Sections.Length && i < ds.Tables.Count; i++)
            {
                var rows = Rows(ds.Tables[i]);
                result[Sections[i]] = i == 0 ? (object)rows.FirstOrDefault() : rows;
            }
            return result;
        }

        public static List<Dictionary<string, object>> Rows(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow r in table.Rows)
            {
                var d = new Dictionary<string, object>();
                foreach (DataColumn c in table.Columns)
                {
                    var v = r[c];
                    var name = char.ToLowerInvariant(c.ColumnName[0]) + c.ColumnName.Substring(1);
                    d[name] = v == DBNull.Value ? null : v is DateTime ? Clock.Iso((DateTime)v) : v;
                }
                list.Add(d);
            }
            return list;
        }
    }
}
