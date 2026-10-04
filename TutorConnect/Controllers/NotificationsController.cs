using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    [RoleAuthorize]
    public class NotificationsController : BaseController
    {
        /// <summary>Sessions and tutorials starting within 20 minutes; polled by site.js for in-app toasts.</summary>
        public ActionResult Reminders()
        {
            if (CurrentRole != "Student" && CurrentRole != "Tutor") return Json(new object[0], JsonRequestBehavior.AllowGet);
            List<Reminder> items;
            try { items = Db.List<Reminder>("sp_Reminders_Upcoming", Db.P("@UserId", CurrentUserId), Db.P("@Role", CurrentRole)); }
            catch (Exception ex) { AppLog.Error("Reminders", ex); return Json(new object[0], JsonRequestBehavior.AllowGet); }
            return Json(items.Select(r => new
            {
                key = r.Key,
                title = r.Title,
                time = r.StartTime.ToString("HH:mm"),
                minutes = (int)Math.Max(0, Math.Ceiling((r.StartTime - TutorConnect.Helpers.Clock.Now).TotalMinutes))
            }), JsonRequestBehavior.AllowGet);
        }

        /// <summary>The top-bar notification centre: latest activity plus how many are unread.</summary>
        public ActionResult Recent()
        {
            System.Data.DataSet ds;
            try { ds = Db.QuerySet("sp_Notifications_Get", Db.P("@UserId", CurrentUserId)); }
            catch (Exception ex)
            {
                AppLog.Error("Notifications", ex);
                return Json(new { unread = 0, items = new object[0] }, JsonRequestBehavior.AllowGet);
            }
            var items = Db.Map<ActivityItem>(ds.Tables[0]);
            var unread = ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 ? Convert.ToInt32(ds.Tables[1].Rows[0][0]) : 0;
            return Json(new
            {
                unread,
                items = items.Select(a => new
                {
                    id = a.ActivityId,
                    message = a.Message,
                    ago = Helpers.Ui.Ago(a.CreatedAt),
                    unread = a.IsUnread,
                    url = Url.Action("Open", new { id = a.ActivityId })
                }),
                allUrl = Url.Action("Index")
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Every notification with its date, time and where it leads.</summary>
        public ActionResult Index()
        {
            var items = new List<ActivityItem>();
            try { items = Db.List<ActivityItem>("sp_Notifications_List", Db.P("@UserId", CurrentUserId), Db.P("@Take", 200)); }
            catch (Exception ex) { AppLog.Error("Notifications/Index", ex); FlashError("Your notifications couldn't be loaded right now."); }
            // opening this page counts as seeing them
            try { Db.Exec("sp_Notifications_MarkSeen", Db.P("@UserId", CurrentUserId)); } catch (Exception ex) { AppLog.Error("Notifications/MarkSeen", ex); }
            ViewBag.Links = items.ToDictionary(a => a.ActivityId, a => NotificationLinks.For(CurrentRole, a.Message, a.Link));
            ViewBag.Highlight = Request.QueryString["id"];
            return View(items);
        }

        /// <summary>Clicking a notification: go to the page it's about, or show its details when there's no page.</summary>
        public ActionResult Open(int id)
        {
            ActivityItem item = null;
            try { item = Db.Single<ActivityItem>("sp_Notification_Get", Db.P("@ActivityId", id), Db.P("@UserId", CurrentUserId)); }
            catch (Exception ex) { AppLog.Error("Notifications/Open", ex); }
            if (item == null) { FlashError("That notification isn't available."); return RedirectToAction("Index"); }
            var link = NotificationLinks.For(CurrentRole, item.Message, item.Link);
            if (link == NotificationLinks.Fallback) return RedirectToAction("Index", new { id });   // details on the list
            return Redirect(Url.Content("~" + link));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkSeen()
        {
            try { Db.Exec("sp_Notifications_MarkSeen", Db.P("@UserId", CurrentUserId)); }
            catch (Exception ex) { AppLog.Error("Notifications/MarkSeen", ex); return Json(new { ok = false }); }
            return Json(new { ok = true });
        }
    }
}
