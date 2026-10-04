using System;
using System.Threading;
using TutorConnect.Data;

namespace TutorConnect.Services
{
    /// <summary>
    /// When a tutorial is over, students on the register who didn't check in are marked absent (method "Auto")
    /// and notified. Runs every 5 minutes in the background, and at most once a minute from page requests, so
    /// registers are up to date even if the site was idle. Tutors can still change any mark afterwards.
    /// </summary>
    public static class AttendanceAutoMarker
    {
        private static readonly object Sync = new object();
        private static DateTime _lastRunUtc = DateTime.MinValue;
        private static Timer _timer;
        private static bool _loggedFailure;

        /// <summary>Starts the background check (called once from Application_Start).</summary>
        public static void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(_ => RunIfDue(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        }

        /// <summary>Marks absences if the last run was more than a minute ago. Never throws.</summary>
        public static int RunIfDue()
        {
            lock (Sync)
            {
                if ((DateTime.UtcNow - _lastRunUtc).TotalSeconds < 60) return 0;
                _lastRunUtc = DateTime.UtcNow;
            }
            try
            {
                var marked = Db.ScalarInt("sp_Tutorials_AutoMarkAbsent");
                if (marked > 0) AppLog.Write("ATTENDANCE", marked + " student(s) marked absent automatically (didn't check in).");
                _loggedFailure = false;
                return marked;
            }
            catch (Exception ex)
            {
                // database not installed or upgraded yet: try again later, and log only once
                if (!_loggedFailure) { AppLog.Error("AttendanceAutoMarker", ex); _loggedFailure = true; }
                return 0;
            }
        }
    }
}
