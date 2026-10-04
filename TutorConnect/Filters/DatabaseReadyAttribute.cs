using System;
using System.Web.Mvc;
using System.Web.Routing;
using TutorConnect.Services;

namespace TutorConnect.Filters
{
    /// <summary>
    /// Before any page that needs the database runs, make sure the database is installed. If it isn't,
    /// send the visitor to /Setup instead of letting a SQL error surface (and stop the debugger).
    /// Once the database is confirmed ready the check is skipped for the life of the app.
    /// </summary>
    public class DatabaseReadyAttribute : ActionFilterAttribute
    {
        private static volatile bool _ready;
        private static DateTime _lastCheck = DateTime.MinValue;
        private static bool _upgradeTried;
        private static bool _demoBankingDone;
        private static readonly object Sync = new object();

        public static void Reset() { _ready = false; _lastCheck = DateTime.MinValue; _upgradeTried = false; _demoBankingDone = false; }

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (_ready && !_demoBankingDone)
            {
                // once per start: encrypt demo tutors' account numbers that the SQL scripts left empty
                _demoBankingDone = true;
                try { DatabaseSetup.EnsureDemoBanking(); }
                catch (Exception ex) { AppLog.Error("EnsureDemoBanking", ex); }
            }
            if (_ready) AttendanceAutoMarker.RunIfDue();   // at most once a minute; never throws
            if (_ready || !DatabaseSetup.Enabled) return;

            var controller = ctx.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (controller == "Setup" || controller == "Home") return;
            if (controller == "ApiV1" && ctx.ActionDescriptor.ActionName == "Health") return;
            if (controller == "Payment" && ctx.ActionDescriptor.ActionName == "Notify") return;

            bool ready;
            lock (Sync)
            {
                if (_ready) return;
                // While not ready, re-check at most every few seconds.
                if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 10) ready = false;
                else
                {
                    _lastCheck = DateTime.UtcNow;
                    var status = DatabaseSetup.Check();
                    // An older install is brought up to date automatically; this never deletes data.
                    if (!status.Ready && status.NeedsUpgrade && status.ScriptsFolder != null && !_upgradeTried)
                    {
                        _upgradeTried = true;   // once per run: a failing upgrade must not slow every request
                        try { DatabaseSetup.Upgrade(); status = DatabaseSetup.Check(); }
                        catch (Exception ex) { AppLog.Error("Automatic database upgrade", ex); }
                    }
                    ready = status.Ready;
                    _ready = ready;
                }
            }
            if (ready) return;

            if (ctx.HttpContext.Request.IsAjaxRequest() || (ctx.HttpContext.Request.Path ?? "").StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Result = new JsonResult { Data = new { ok = false, message = "The database isn't installed yet. Open /Setup." }, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                ctx.HttpContext.Response.StatusCode = 503;
                return;
            }
            ctx.Result = new RedirectToRouteResult(new RouteValueDictionary { { "controller", "Setup" }, { "action", "Index" } });
        }
    }
}
