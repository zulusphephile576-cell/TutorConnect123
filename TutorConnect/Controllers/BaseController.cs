using System;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    [NoCache, ValidateInput(false)]
    public abstract class BaseController : Controller
    {
        protected int CurrentUserId => (Session["UserId"] as int?) ?? 0;
        protected string CurrentRole => Session["Role"] as string;
        protected string CurrentName => Session["Name"] as string;
        protected int CurrentYear => (Session["YearOfStudy"] as int?) ?? 0;
        protected bool IsLoggedIn => Session["UserId"] is int;

        protected void Flash(string message, string type = "success")
        {
            TempData["Flash"] = message;
            TempData["FlashType"] = type;
        }

        protected void FlashError(string message) => Flash(message, "error");

        protected ActionResult DashboardFor(string role)
        {
            switch (role)
            {
                case "Student": return RedirectToAction("Dashboard", "Student");
                case "Tutor": return RedirectToAction("Dashboard", "Tutor");
                case "Lecturer": return RedirectToAction("Dashboard", "Lecturer");
                case "Admin": return RedirectToAction("Dashboard", "Admin");
                default: return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Runs a database action. Stored-procedure validation errors (THROW 5xxxx) become a flash message;
        /// anything else is logged and shown as a generic error. Returns true on success.
        /// </summary>
        protected bool TryDb(Action action, string successMessage = null)
        {
            try
            {
                action();
                if (successMessage != null) Flash(successMessage);
                return true;
            }
            catch (Exception ex)
            {
                string message;
                if (Db.IsUserError(ex, out message))
                {
                    FlashError(message);
                }
                else
                {
                    AppLog.Error(ControllerContext.RouteData.Values["controller"] + "/" + ControllerContext.RouteData.Values["action"], ex);
                    FlashError("Something went wrong on our side. Please try again.");
                }
                return false;
            }
        }

        /// <summary>Same as TryDb but reports the error as a model error so the form is redisplayed.</summary>
        protected bool TryDbForm(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                string message;
                if (Db.IsUserError(ex, out message))
                {
                    ModelState.AddModelError("", message);
                }
                else
                {
                    AppLog.Error(ControllerContext.RouteData.Values["controller"] + "/" + ControllerContext.RouteData.Values["action"], ex);
                    ModelState.AddModelError("", "Something went wrong on our side. Please try again.");
                }
                return false;
            }
        }

        protected void LogActivity(int userId, string message)
        {
            try
            {
                Db.Exec("sp_Activity_Add", Db.P("@UserId", userId), Db.P("@Message", message));
            }
            catch (Exception ex)
            {
                AppLog.Error("LogActivity", ex);
            }
        }

        protected ActionResult JsonError(string message, int status = 400)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { ok = false, message });
        }
    }
}
