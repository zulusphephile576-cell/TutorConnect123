using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using TutorConnect.Models;

namespace TutorConnect.Services
{
    /// <summary>
    /// Consumes the public Nager.Date REST API (https://date.nager.at) for South African public holidays.
    /// Results are cached for 12 hours. If the API can't be reached, a built-in calendar is used so the
    /// schedule and availability warnings keep working offline.
    /// </summary>
    public static class HolidayService
    {
        public const string ApiUrl = "https://date.nager.at/api/v3/PublicHolidays/{0}/ZA";

        private class CacheEntry { public List<PublicHoliday> Items; public DateTime FetchedUtc; public string Source; public double TtlHours; }

        /// <summary>WebClient with a short timeout, so a missing internet connection never stalls a page.</summary>
        private class TimedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null) request.Timeout = 4000;
                return request;
            }
        }
        private static readonly ConcurrentDictionary<int, CacheEntry> Cache = new ConcurrentDictionary<int, CacheEntry>();

        public static List<PublicHoliday> ForYear(int year)
        {
            string source;
            return ForYear(year, out source);
        }

        public static List<PublicHoliday> ForYear(int year, out string source)
        {
            CacheEntry cached;
            if (Cache.TryGetValue(year, out cached) && (DateTime.UtcNow - cached.FetchedUtc).TotalHours < cached.TtlHours)
            {
                source = cached.Source;
                return cached.Items;
            }

            List<PublicHoliday> items = null;
            source = "Nager.Date public holidays API";
            if (AppConfig.GetBool("Holidays:UseApi", true))
            {
                try
                {
                    using (var client = new TimedWebClient { Encoding = Encoding.UTF8 })
                    {
                        client.Headers[HttpRequestHeader.Accept] = "application/json";
                        client.Headers[HttpRequestHeader.UserAgent] = "TutorConnect/1.0";
                        items = ParseNager(client.DownloadString(string.Format(CultureInfo.InvariantCulture, ApiUrl, year)));
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Write("HOLIDAYS", "Nager.Date API unavailable (" + ex.GetBaseException().Message + "); using built-in calendar.");
                    items = null;
                }
            }
            if (items == null || items.Count == 0)
            {
                items = BuiltIn(year);
                source = "built-in South African calendar";
            }
            // Live data is kept for 12 hours; after a failure, try the live API again in 30 minutes.
            var ttl = source.StartsWith("Nager") ? 12 : 0.5;
            Cache[year] = new CacheEntry { Items = items, FetchedUtc = DateTime.UtcNow, Source = source, TtlHours = ttl };
            return items;
        }

        public static PublicHoliday On(DateTime date)
        {
            return ForYear(date.Year).FirstOrDefault(h => h.Date == date.Date);
        }

        public static List<PublicHoliday> Between(DateTime from, DateTime toExclusive)
        {
            var list = new List<PublicHoliday>();
            for (var y = from.Year; y <= toExclusive.AddDays(-1).Year; y++)
                list.AddRange(ForYear(y).Where(h => h.Date >= from.Date && h.Date < toExclusive.Date));
            return list;
        }

        /// <summary>Parses the Nager.Date v3 response: [{ "date": "2026-01-01", "localName": "...", "name": "..." }, ...].</summary>
        public static List<PublicHoliday> ParseNager(string json)
        {
            var result = new List<PublicHoliday>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            var rows = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json);
            if (rows == null) return result;
            foreach (var r in rows)
            {
                object d, n;
                DateTime date;
                if (!r.TryGetValue("date", out d) || !DateTime.TryParseExact(Convert.ToString(d, CultureInfo.InvariantCulture), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) continue;
                if (!r.TryGetValue("name", out n) && !r.TryGetValue("localName", out n)) continue;
                result.Add(new PublicHoliday { Date = date, Name = Convert.ToString(n, CultureInfo.InvariantCulture) });
            }
            return result.OrderBy(h => h.Date).ToList();
        }

        /// <summary>
        /// South Africa's statutory public holidays (Public Holidays Act 36 of 1994), including the rule that a
        /// holiday falling on a Sunday is observed on the Monday.
        /// </summary>
        public static List<PublicHoliday> BuiltIn(int year)
        {
            var easter = EasterSunday(year);
            var fixedDays = new List<PublicHoliday>
            {
                H(year, 1, 1, "New Year's Day"),
                H(year, 3, 21, "Human Rights Day"),
                new PublicHoliday { Date = easter.AddDays(-2), Name = "Good Friday" },
                new PublicHoliday { Date = easter.AddDays(1), Name = "Family Day" },
                H(year, 4, 27, "Freedom Day"),
                H(year, 5, 1, "Workers' Day"),
                H(year, 6, 16, "Youth Day"),
                H(year, 8, 9, "National Women's Day"),
                H(year, 9, 24, "Heritage Day"),
                H(year, 12, 16, "Day of Reconciliation"),
                H(year, 12, 25, "Christmas Day"),
                H(year, 12, 26, "Day of Goodwill")
            };
            var all = new List<PublicHoliday>(fixedDays);
            foreach (var h in fixedDays.Where(h => h.Date.DayOfWeek == DayOfWeek.Sunday))
            {
                var monday = h.Date.AddDays(1);
                if (all.All(x => x.Date != monday)) all.Add(new PublicHoliday { Date = monday, Name = "Public holiday (" + h.Name + " observed)" });
            }
            return all.OrderBy(h => h.Date).ToList();
        }

        /// <summary>Western Easter Sunday (anonymous Gregorian algorithm).</summary>
        public static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31, day = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        private static PublicHoliday H(int y, int m, int d, string name)
        {
            return new PublicHoliday { Date = new DateTime(y, m, d), Name = name };
        }
    }
}
