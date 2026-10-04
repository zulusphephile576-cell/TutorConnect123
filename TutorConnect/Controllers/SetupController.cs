using System;
using System.Web.Mvc;
using TutorConnect.Filters;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    /// <summary>
    /// Database health check and one-click installer. Open to anyone only while the database is missing or
    /// incomplete; once it's installed, only a signed-in administrator can reach it (reinstalling wipes data).
    /// Turn it off completely with the Setup:Enabled appSetting.
    /// </summary>
    public class SetupController : BaseController
    {
        private bool Allowed(DatabaseSetup.Status status)
        {
            return DatabaseSetup.Enabled && (!status.Ready || CurrentRole == "Admin");
        }

        [HttpGet]
        public ActionResult Index()
        {
            var status = DatabaseSetup.Check();
            if (!Allowed(status)) return HttpNotFound();
            return View(status);
        }

        /// <summary>Adds the extra demo people (administrators only, on an installed database).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddDemoUsers(string returnUrl)
        {
            if (CurrentRole != "Admin" || !DatabaseSetup.Check().Ready) return HttpNotFound();
            try
            {
                var log = DatabaseSetup.AddDemoUsers();
                Flash(string.Join(" ", log) + " Every new account's password is Password@123.");
            }
            catch (Exception ex)
            {
                AppLog.Error("Setup/AddDemoUsers", ex);
                FlashError("Demo users couldn't be added: " + ex.GetBaseException().Message);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Users", "Admin");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Upgrade()
        {
            var status = DatabaseSetup.Check();
            if (!Allowed(status)) return HttpNotFound();
            try
            {
                TempData["SetupLog"] = DatabaseSetup.Upgrade();
                DatabaseReadyAttribute.Reset();
                Flash("Database updated. All existing accounts and data were kept.");
            }
            catch (Exception ex)
            {
                AppLog.Error("Setup/Upgrade", ex);
                FlashError("Update stopped: " + ex.GetBaseException().Message);
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Install(bool includeDemoData = true, bool confirmWipe = false)
        {
            var status = DatabaseSetup.Check();
            if (!Allowed(status)) return HttpNotFound();
            if (!status.ServerReachable)
            {
                FlashError("SQL Server can't be reached yet. Fix the connection first.");
                return RedirectToAction("Index");
            }
            if (status.TablesInstalled && status.UserCount > 0 && !confirmWipe)
            {
                FlashError("The database already has data. Tick the box to confirm you want to erase it and reinstall.");
                return RedirectToAction("Index");
            }

            try
            {
                var log = DatabaseSetup.Install(includeDemoData);
                if (includeDemoData)
                {
                    try { log.AddRange(DatabaseSetup.AddDemoUsers()); }
                    catch (Exception ex) { AppLog.Error("Setup/Install demo users", ex); log.Add("Extra demo users couldn't be added: " + ex.GetBaseException().Message); }
                }
                TempData["SetupLog"] = log;
                DatabaseReadyAttribute.Reset();
                Session.Clear();
                Flash("Database installed. Sign in with admin@tutorconnect.ac.za and Admin@123"
                      + (includeDemoData ? ", or any of the demo accounts." : "."));
            }
            catch (Exception ex)
            {
                AppLog.Error("Setup/Install", ex);
                FlashError("Installation stopped: " + ex.GetBaseException().Message);
            }
            return RedirectToAction("Index");
        }
    }
}
