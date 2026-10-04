namespace TutorConnect.Services
{
    /// <summary>
    /// Decides which page a notification opens. New notifications carry their own link (ActivityLog.Link);
    /// older ones are matched on their wording, per role. Anything unrecognised opens the notifications page.
    /// </summary>
    public static class NotificationLinks
    {
        public const string Fallback = "/Notifications";

        public static string For(string role, string message, string storedLink)
        {
            if (!string.IsNullOrWhiteSpace(storedLink) && storedLink.StartsWith("/")) return Fix(role, storedLink);
            var m = (message ?? "").ToLowerInvariant();

            if (m.Contains("banking details")) return role == "Tutor" ? "/Tutor/Banking" : Fallback;
            if (m.Contains("payout")) return role == "Tutor" ? "/Tutor/Reports" : role == "Admin" ? "/Admin/Transactions" : Fallback;
            if (m.Contains("course material") || m.Contains("course content"))
                return role == "Student" ? "/Student/Content" : role == "Tutor" ? "/Tutor/Content" : role == "Lecturer" ? "/Lecturer/Content" : Fallback;
            if (m.Contains("application") || m.Contains("tutor team"))
            {
                if (role == "Lecturer") return "/Lecturer/Applications";
                if (role == "Admin") return "/Admin/Applications";
                if (m.Contains("tutor team")) return "/Tutor/Dashboard";
                return "/TutorApplication/Status";
            }
            if (m.Contains("assigned to tutor")) return role == "Tutor" ? "/Tutor/Modules" : Fallback;
            if (m.Contains("tutorial") || m.Contains("checked in"))
                return role == "Student" ? "/Student/Tutorials" : role == "Tutor" ? "/Tutor/Tutorials" : role == "Lecturer" ? "/Lecturer/Tutorials" : Fallback;
            if (m.Contains("announcement")) return role == "Student" ? "/Student/Dashboard#announcements" : role == "Tutor" ? "/Tutor/Announcements" : Fallback;
            if (m.Contains("booking") || m.Contains("session") || m.Contains("payment") || m.Contains("you paid"))
            {
                if (role == "Student") return "/Student/Bookings";
                if (role == "Tutor") return "/Tutor/Sessions";
                if (role == "Admin") return "/Admin/Transactions";
                return Fallback;
            }
            if (m.Contains("password") || m.Contains("profile") || m.Contains("deletion") || m.Contains("personal information"))
                return "/Account/Settings";
            if (m.Contains("welcome")) return "/" + (role ?? "Home") + "/Dashboard";
            return Fallback;
        }

        /// <summary>A stored student link opened by another role (e.g. a tutor) goes to that role's own page.</summary>
        private static string Fix(string role, string link)
        {
            if (link == "/Student/Content" && role == "Tutor") return "/Tutor/Content";
            if (link == "/Student/Content" && role == "Lecturer") return "/Lecturer/Content";
            return link;
        }
    }
}
