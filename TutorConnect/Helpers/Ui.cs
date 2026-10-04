using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace TutorConnect.Helpers
{
    /// <summary>Small presentation helpers used across all views.</summary>
    public static class Ui
    {
        private static readonly string[] Palette = { "#4F46E5", "#6C63FF", "#0E7490", "#15803D", "#B45309", "#BE123C", "#7C3AED", "#0369A1", "#4D7C0F" };

        public static IHtmlString Badge(string status)
        {
            if (string.IsNullOrEmpty(status)) return new HtmlString("<span class=\"badge badge-muted\">Not marked</span>");
            string tone;
            switch (status)
            {
                case "Present": case "Confirmed": case "Approved": case "Complete": case "Sent": case "Completed": case "Recommended":
                    tone = "success"; break;
                case "Pending": case "Late": case "Submitted": case "UnderReview": case "Queued":
                    tone = "warning"; break;
                case "Absent": case "Declined": case "Cancelled": case "Rejected": case "Failed": case "NotRecommended": case "NoBankDetails":
                    tone = "danger"; break;
                default:
                    tone = "info"; break;
            }
            var label = status == "NoBankDetails" ? "Awaiting bank details" : Regex.Replace(status, "([a-z])([A-Z])", "$1 $2");
            label = label.Length > 1 ? label.Substring(0, 1) + label.Substring(1).ToLowerInvariant() : label;
            return new HtmlString("<span class=\"badge badge-" + tone + "\">" + HttpUtility.HtmlEncode(label) + "</span>");
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return (parts[0].Substring(0, 1) + (parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : "")).ToUpperInvariant();
        }

        public static string AvatarColor(string name)
        {
            unchecked
            {
                int hash = 17;
                foreach (var c in name ?? "") hash = hash * 31 + c;
                return Palette[(hash & 0x7fffffff) % Palette.Length];
            }
        }

        public static IHtmlString Avatar(string name, string size = "")
        {
            return new HtmlString(string.Format("<span class=\"avatar {0}\" style=\"background:{1}\" aria-hidden=\"true\">{2}</span>",
                size, AvatarColor(name), HttpUtility.HtmlEncode(Initials(name))));
        }

        /// <summary>South African Rand in the local convention: R1 050,00 (non-breaking space for thousands, comma for cents).</summary>
        public static string Money(decimal value)
        {
            var neg = value < 0;
            var text = Math.Abs(value).ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(",", "\u00A0").Replace(".", ",");
            return (neg ? "-R" : "R") + text;
        }

        public static string Date(DateTime d) => d.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

        public static string DateTime(DateTime d) => d.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);

        public static string Time(DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);

        public static string TimeRange(DateTime start, DateTime end) =>
            start.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture) + " \u2013 " + end.ToString("HH:mm", CultureInfo.InvariantCulture);

        public static string Ago(DateTime d)
        {
            var span = TutorConnect.Helpers.Clock.Now - d;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " min ago";
            if (span.TotalHours < 24) return (int)span.TotalHours + " h ago";
            if (span.TotalDays < 7) return (int)span.TotalDays + (span.TotalDays < 2 ? " day ago" : " days ago");
            return d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        public static string Percent(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";

        public static string RateTone(decimal rate) => rate >= 75 ? "success" : rate >= 50 ? "warning" : "danger";

        public static int Pct(int part, int total) => total <= 0 ? 0 : (int)Math.Round(100.0 * part / total);
    
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> AssetVersions =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        /// <summary>
        /// URL for a static file with its last-changed time appended (?v=...), so browsers can cache it for a long time
        /// and still fetch a new copy as soon as the file changes.
        /// </summary>
        public static string Asset(string virtualPath)
        {
            var url = System.Web.VirtualPathUtility.ToAbsolute(virtualPath);
            var version = AssetVersions.GetOrAdd(virtualPath, vp =>
            {
                try
                {
                    var file = System.Web.Hosting.HostingEnvironment.MapPath(vp);
                    return file != null && System.IO.File.Exists(file) ? System.IO.File.GetLastWriteTimeUtc(file).Ticks.ToString("x") : "1";
                }
                catch { return "1"; }
            });
            return url + "?v=" + version;
        }
    }
}
