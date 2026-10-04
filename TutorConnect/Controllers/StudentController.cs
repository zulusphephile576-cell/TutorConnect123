using System;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    [RoleAuthorize("Student")]
    public class StudentController : BaseController
    {
        public ActionResult Dashboard()
        {
            var ds = Db.QuerySet("sp_Student_Dashboard", Db.P("@StudentId", CurrentUserId));
            var model = new DashboardViewModel
            {
                Sessions = Db.Map<Booking>(ds.Tables[0]),
                Tutorials = Db.Map<Tutorial>(ds.Tables[1]),
                Activity = Db.Map<ActivityItem>(ds.Tables[2]),
                Announcements = Db.Map<Announcement>(ds.Tables[3]),
                Tutors = Db.Map<TutorAssignment>(ds.Tables[4]),
                NewSlots = Db.Map<Slot>(ds.Tables[5]),
                Stats = Db.Row(ds.Tables[6])
            };
            return View(model);
        }

        // ---------- Find & book ----------
        public ActionResult FindTutor(string q)
        {
            ViewBag.Query = q;
            ViewBag.Year = CurrentYear;
            return View(Db.List<TutorAssignment>("sp_Student_SearchTutors", Db.P("@StudentId", CurrentUserId), Db.P("@Term", q)));
        }

        [HttpGet]
        public ActionResult Book(int tutorId, int? moduleId)
        {
            var model = BuildBookModel(tutorId);
            if (model == null)
            {
                FlashError("That tutor doesn't cover any of your modules for your year of study.");
                return RedirectToAction("FindTutor");
            }
            model.ModuleId = moduleId ?? (model.Modules.Count == 1 ? model.Modules[0].ModuleId : (int?)null);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Book(BookViewModel model)
        {
            var fresh = BuildBookModel(model.TutorId);
            if (fresh == null)
            {
                FlashError("That tutor doesn't cover any of your modules for your year of study.");
                return RedirectToAction("FindTutor");
            }
            fresh.ModuleId = model.ModuleId;
            fresh.SlotId = model.SlotId;
            fresh.Hours = model.Hours;
            if (!ModelState.IsValid) return View(fresh);

            var reference = PayFastService.NewReference();
            var ok = TryDbForm(() => Db.ScalarInt("sp_Payment_Create",
                Db.P("@PaymentRef", reference), Db.P("@StudentId", CurrentUserId), Db.P("@TutorId", model.TutorId),
                Db.P("@ModuleId", model.ModuleId), Db.P("@SlotId", model.SlotId), Db.P("@Hours", model.Hours),
                Db.P("@HourlyRate", AppConfig.HourlyRate)));
            if (!ok) return View(fresh);

            return RedirectToAction("Checkout", "Payment", new { reference });
        }

        private BookViewModel BuildBookModel(int tutorId)
        {
            var modules = Db.List<TutorAssignment>("sp_Student_SearchTutors", Db.P("@StudentId", CurrentUserId), Db.P("@Term", null))
                .Where(a => a.TutorId == tutorId).ToList();
            if (modules.Count == 0) return null;
            return new BookViewModel
            {
                TutorId = tutorId,
                TutorName = modules[0].TutorName,
                Modules = modules,
                Slots = Db.List<Slot>("sp_Tutor_OpenSlots", Db.P("@TutorId", tutorId)),
                HourlyRate = AppConfig.HourlyRate,
                TutorCanBePaid = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", tutorId)) != null
            };
        }

        /// <summary>Notifies (and emails) a tutor who can't be paid yet because their bank account is missing.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RequestTutorBanking(int tutorId)
        {
            try
            {
                var me = Db.Single<User>("sp_User_GetById", Db.P("@UserId", CurrentUserId));
                var row = Db.Row(Db.Query("sp_Tutor_BankingNeeded", Db.P("@TutorId", tutorId), Db.P("@StudentName", me.FullName)));
                if (row.Count > 0 && row["EmailNotifications"] != null && Convert.ToBoolean(row["EmailNotifications"]))
                    EmailService.SendInBackground(Convert.ToString(row["Email"]), "Add your banking details to get booked",
                        "<p>Hi " + EmailService.Encode(Convert.ToString(row["FirstName"])) + ",</p><p>" + EmailService.Encode(me.FullName) +
                        " wants to book a session with you, but payments can't be made to you until you add your bank account.</p>" +
                        "<p><a href=\"" + AppConfig.BaseUrl + "/Tutor/Banking\" style=\"color:#4F46E5\">Add my banking details</a></p>");
                Flash("We've asked the tutor to add their banking details. You'll be able to book once they have.", "info");
            }
            catch (Exception ex)
            {
                AppLog.Error("RequestTutorBanking", ex);
                FlashError("The request couldn't be sent. Try again in a moment.");
            }
            return RedirectToAction("Book", new { tutorId });
        }

        // ---------- Bookings ----------
        public ActionResult Bookings()
        {
            ViewBag.HourlyRate = AppConfig.HourlyRate;
            return View(Db.List<Booking>("sp_Student_Bookings", Db.P("@StudentId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int id)
        {
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Booking_CancelByStudent", Db.P("@BookingId", id), Db.P("@StudentId", CurrentUserId))))
            {
                if (affected == 1) Flash("Your session was cancelled and the tutor has been notified.");
                else FlashError("Only upcoming pending or confirmed sessions can be cancelled.");
            }
            return RedirectToAction("Bookings");
        }

        // ---------- Tutorials ----------
        public ActionResult Tutorials()
        {
            return View(Db.List<Tutorial>("sp_Student_Tutorials", Db.P("@StudentId", CurrentUserId)));
        }

        public ActionResult Tutorial(int id)
        {
            var tutorial = Db.Single<Tutorial>("sp_Tutorial_Get", Db.P("@TutorialId", id), Db.P("@StudentId", CurrentUserId));
            if (tutorial == null || !tutorial.IsEnrolled) return HttpNotFound();
            return View(tutorial);
        }

        // ---------- QR check-in ----------
        /// <summary>
        /// Opened by scanning a tutorial's QR code. Checks the signed-in student in straight away and marks them present.
        /// The student comes only from the session; the token only identifies the tutorial (a different token per tutorial).
        /// Students who aren't signed in are sent to sign in and brought back here automatically.
        /// </summary>
        [HttpGet]
        public ActionResult CheckIn(string token)
        {
            Guid guid;
            if (!Guid.TryParse(token, out guid)) return View("CheckInResult", new CheckInResult { Result = "NotFound" });
            return CheckInResultFor(() => Db.Single<CheckInResult>("sp_Tutorial_QrCheckIn", Db.P("@Token", guid), Db.P("@StudentId", CurrentUserId)));
        }

        /// <summary>For students who can't scan: they type the short code shown under the QR.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CheckInCode(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant().Replace(" ", "");
            if (code.Length < 4 || code.Length > 8)
            {
                FlashError("Type the code shown under the QR code, for example K7PM2X.");
                return RedirectToAction("Tutorials");
            }
            return CheckInResultFor(() => Db.Single<CheckInResult>("sp_Tutorial_CheckInByCode", Db.P("@Code", code), Db.P("@StudentId", CurrentUserId)));
        }

        private ActionResult CheckInResultFor(Func<CheckInResult> run)
        {
            CheckInResult result = null;
            if (!TryDb(() => result = run())) return RedirectToAction("Tutorials");
            result = result ?? new CheckInResult { Result = "NotFound" };
            if (result.TutorialId.HasValue)
                ViewBag.Tutorial = Db.Single<Tutorial>("sp_Tutorial_Get", Db.P("@TutorialId", result.TutorialId.Value), Db.P("@StudentId", CurrentUserId));
            return View("CheckInResult", result);
        }

        private static int? TutorialIdForToken(Guid token)
        {
            var t = Db.Single<Tutorial>("sp_Tutorial_GetByToken", Db.P("@Token", token));
            return t == null ? (int?)null : t.TutorialId;
        }

        // ---------- Weekly schedule ----------
        public ActionResult Schedule(DateTime? week)
        {
            var model = new ScheduleViewModel { WeekStart = ScheduleViewModel.MondayOf(week ?? TutorConnect.Helpers.Clock.Today), Role = "Student" };
            var end = model.WeekStart.AddDays(7);

            foreach (var b in Db.List<Booking>("sp_Student_Bookings", Db.P("@StudentId", CurrentUserId)))
                if (b.StartTime < end && b.EndTime > model.WeekStart && (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "Completed"))
                    model.Events.Add(new CalendarEvent { Kind = "Session", Code = b.ModuleCode, Title = "Session with " + b.TutorName, Subtitle = b.ModuleName,
                        Start = b.StartTime, End = b.EndTime, Status = b.Status, Url = Url.Action("Bookings") });

            foreach (var t in Db.List<Tutorial>("sp_Student_Tutorials", Db.P("@StudentId", CurrentUserId)))
                if (t.StartTime < end && t.EndTime > model.WeekStart)
                    model.Events.Add(new CalendarEvent { Kind = "Tutorial", Code = t.ModuleCode, Title = t.Title, Subtitle = string.IsNullOrEmpty(t.Venue) ? t.TutorName : t.Venue,
                        Start = t.StartTime, End = t.EndTime, Status = t.MyStatus, Url = Url.Action("Tutorial", new { id = t.TutorialId }) });

            try { model.Holidays = HolidayService.Between(model.WeekStart, end); }
            catch (Exception ex) { AppLog.Error("Schedule holidays", ex); }
            return View("~/Views/Shared/Schedule.cshtml", model);
        }

        // ---------- Attendance & modules ----------
        public ActionResult Attendance()
        {
            return View(Db.List<AttendanceRecord>("sp_Student_Attendance", Db.P("@StudentId", CurrentUserId)));
        }

        public ActionResult Modules()
        {
            return View(Db.List<Module>("sp_Student_Modules", Db.P("@StudentId", CurrentUserId)));
        }

        /// <summary>Course material sent to the student's modules: lecturer uploads, and tutor uploads the lecturer approved.</summary>
        [ActionName("Content")]
        public ActionResult CourseContent(string module)
        {
            var items = Db.List<ContentItem>("sp_Content_ForStudent", Db.P("@StudentId", CurrentUserId));
            ViewBag.Module = module;
            return View("Content", items);
        }
    }
}
