using System;
using System.Globalization;

namespace TutorConnect.Helpers
{
    /// <summary>
    /// South African time for everything the app shows or compares (SAST, UTC+2, no daylight saving),
    /// regardless of the server's own time zone. The database uses dbo.fn_SaNow() for the same reason.
    /// </summary>
    public static class Clock
    {
        private static readonly TimeZoneInfo Sast = FindSast();

        private static TimeZoneInfo FindSast()
        {
            foreach (var id in new[] { "South Africa Standard Time", "Africa/Johannesburg" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            // South Africa has been UTC+2 without daylight saving since 1944.
            return TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "South Africa Standard Time", "SAST");
        }

        public static DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Sast), DateTimeKind.Unspecified);

        public static DateTime Today => Now.Date;

        public const string ZoneLabel = "SAST";

        /// <summary>ISO 8601 with the +02:00 offset, used by the REST API.</summary>
        public static string Iso(DateTime sastTime) => sastTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+02:00";

        public static string Iso(DateTime? sastTime) => sastTime.HasValue ? Iso(sastTime.Value) : null;
    }
}
