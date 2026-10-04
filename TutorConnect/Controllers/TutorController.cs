using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Helpers;
using TutorConnect.Models;
using TutorConnect.Services;
using TutorConnect.Services.Payouts;

namespace TutorConnect.Controllers
{
    [RoleAuthorize("Tutor")]
    public class TutorController : BaseController
    {
        private static readonly string[] AttendanceStatuses = { "Present", "Absent" };

        public ActionResult Dashboard()
        {
            var ds = Db.QuerySet("sp_Tutor_Dashboard", Db.P("@TutorId", CurrentUserId));
            var model = new DashboardViewModel
            {
                TodaySessions = Db.Map<Booking>(ds.Tables[0]),
                Sessions = Db.Map<Booking>(ds.Tables[1]),
                PendingRequests = Db.Map<Booking>(ds.Tables[2]),
                NewContent = Db.Map<ContentItem>(ds.Tables[3]),
                Tutorials = Db.Map<Tutorial>(ds.Tables[4]),
                Stats = Db.Row(ds.Tables[5]),
                Activity = Db.Map<ActivityItem>(ds.Tables[6])
            };
            ViewBag.HasBanking = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", CurrentUserId)) != null;
            return View(model);
        }

        private System.Collections.Generic.List<TutorAssignment> Assignments()
        {
            return Db.List<TutorAssignment>("sp_Tutor_Assignments", Db.P("@TutorId", CurrentUserId));
        }

        /// <summary>Parses the "moduleId|year" value used by the module/year pickers.</summary>
        private static bool ParseAssignment(string value, out int moduleId, out int year)
        {
            moduleId = 0; year = 0;
            var parts = (value ?? "").Split('|');
            return parts.Length == 2 && int.TryParse(parts[0], out moduleId) && int.TryParse(parts[1], out year);
        }

        // ---------- Weekly schedule ----------
        public ActionResult Schedule(DateTime? week)
        {
            var model = new ScheduleViewModel { WeekStart = ScheduleViewModel.MondayOf(week ?? TutorConnect.Helpers.Clock.Today), Role = "Tutor" };
            var end = model.WeekStart.AddDays(7);

            foreach (var b in Db.List<Booking>("sp_Tutor_Sessions", Db.P("@TutorId", CurrentUserId)))
                if (b.StartTime < end && b.EndTime > model.WeekStart && (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "Completed"))
                    model.Events.Add(new CalendarEvent { Kind = "Session", Code = b.ModuleCode, Title = b.StudentName, Subtitle = b.ModuleName,
                        Start = b.StartTime, End = b.EndTime, Status = b.Status, Url = Url.Action("Sessions") });

            foreach (var t in Db.List<Tutorial>("sp_Tutor_Tutorials", Db.P("@TutorId", CurrentUserId)))
                if (t.StartTime < end && t.EndTime > model.WeekStart)
                    model.Events.Add(new CalendarEvent { Kind = "Tutorial", Code = t.ModuleCode, Title = t.Title, Subtitle = t.Venue,
                        Start = t.StartTime, End = t.EndTime, Url = Url.Action("Register", new { id = t.TutorialId }) });

            foreach (var sl in Db.List<Slot>("sp_Tutor_Slots", Db.P("@TutorId", CurrentUserId)))
                if (!sl.IsBooked && sl.StartTime < end && sl.EndTime > model.WeekStart)
                    model.Events.Add(new CalendarEvent { Kind = "Slot", Title = "Open for booking", Start = sl.StartTime, End = sl.EndTime, Url = Url.Action("Availability") });

            try { model.Holidays = HolidayService.Between(model.WeekStart, end); }
            catch (Exception ex) { AppLog.Error("Schedule holidays", ex); }
            return View("~/Views/Shared/Schedule.cshtml", model);
        }

        // ---------- Availability ----------
        /// <summary>The modules (and years) this tutor is assigned to, with what's happening in each.</summary>
        public ActionResult Modules()
        {
            return View(Db.List<ModuleOverview>("sp_Tutor_ModuleOverview", Db.P("@TutorId", CurrentUserId)));
        }

        public ActionResult Availability()
        {
            try { ViewBag.Holidays = HolidayService.Between(Clock.Today, Clock.Today.AddDays(90)); }
            catch (Exception ex) { AppLog.Error("Availability holidays", ex); ViewBag.Holidays = new List<PublicHoliday>(); }
            return View(Db.List<Slot>("sp_Tutor_Slots", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddSlot(DateTime? date, TimeSpan? startTime, TimeSpan? endTime)
        {
            if (!date.HasValue || !startTime.HasValue || !endTime.HasValue)
            {
                FlashError("Choose a date, start time and end time.");
                return RedirectToAction("Availability");
            }
            var start = date.Value.Date + startTime.Value;
            var end = date.Value.Date + endTime.Value;
            PublicHoliday holiday = null;
            try { holiday = HolidayService.On(start.Date); } catch (Exception ex) { AppLog.Error("Holiday check", ex); }
            if (TryDb(() => Db.Exec("sp_Slot_Add", Db.P("@TutorId", CurrentUserId), Db.P("@StartTime", start), Db.P("@EndTime", end))))
            {
                if (holiday != null)
                    Flash("Slot added for " + start.ToString("ddd d MMM, HH:mm") + ". Note: that's " + holiday.Name + ", a public holiday, so campus may be closed.", "info");
                else
                    Flash("Slot added for " + start.ToString("ddd d MMM, HH:mm") + ". Students can book it now.");
            }
            return RedirectToAction("Availability");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteSlot(int id)
        {
            TryDb(() => Db.Exec("sp_Slot_Delete", Db.P("@SlotId", id), Db.P("@TutorId", CurrentUserId)), "Slot removed.");
            return RedirectToAction("Availability");
        }

        // ---------- Sessions ----------
        public ActionResult Sessions()
        {
            return View(Db.List<Booking>("sp_Tutor_Sessions", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetBookingStatus(int id, string status)
        {
            if (status != "Confirmed" && status != "Declined" && status != "Cancelled") return new HttpStatusCodeResult(400);
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Booking_SetStatus", Db.P("@BookingId", id), Db.P("@TutorId", CurrentUserId), Db.P("@Status", status))))
            {
                if (affected == 1)
                    Flash(status == "Confirmed" ? "Session confirmed. The student has been notified." : "Session " + status.ToLowerInvariant() + ". The slot is open again.");
                else
                    FlashError("That session can no longer be changed.");
            }
            return RedirectToAction("Sessions");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reschedule(int id, DateTime? date, TimeSpan? time)
        {
            if (!date.HasValue || !time.HasValue)
            {
                FlashError("Choose the new date and time.");
                return RedirectToAction("Sessions");
            }
            var start = date.Value.Date + time.Value;
            TryDb(() => Db.Exec("sp_Booking_Reschedule", Db.P("@BookingId", id), Db.P("@TutorId", CurrentUserId), Db.P("@NewStart", start)),
                "Session moved to " + start.ToString("ddd d MMM, HH:mm") + ".");
            return RedirectToAction("Sessions");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkSessionAttendance(int id, string status)
        {
            if (!AttendanceStatuses.Contains(status)) return new HttpStatusCodeResult(400);
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Booking_MarkAttendance", Db.P("@BookingId", id), Db.P("@TutorId", CurrentUserId), Db.P("@Status", status))))
            {
                if (affected == 1) Flash("Attendance saved and the session marked as completed.");
                else FlashError("Attendance can be marked once a confirmed session has started.");
            }
            return RedirectToAction("Sessions");
        }

        // ---------- Tutorials ----------
        public ActionResult Tutorials()
        {
            ViewBag.Form = new TutorialFormViewModel { Assignments = Assignments(), Date = TutorConnect.Helpers.Clock.Today.AddDays(1) };
            return View(Db.List<Tutorial>("sp_Tutor_Tutorials", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateTutorial(TutorialFormViewModel form)
        {
            int moduleId, year;
            if (!ParseAssignment(form.Assignment, out moduleId, out year)) ModelState.AddModelError("Assignment", "Choose a module and year.");
            if (form.StartTime.HasValue && form.EndTime.HasValue && form.EndTime <= form.StartTime)
                ModelState.AddModelError("EndTime", "The end time must be after the start time.");

            if (ModelState.IsValid)
            {
                var start = form.Date.Value.Date + form.StartTime.Value;
                var end = form.Date.Value.Date + form.EndTime.Value;
                var id = 0;
                if (TryDbForm(() => id = Db.ScalarInt("sp_Tutorial_Create", Db.P("@TutorId", CurrentUserId), Db.P("@ModuleId", moduleId),
                        Db.P("@YearOfStudy", year), Db.P("@Title", form.Title.Trim()), Db.P("@Description", form.Description),
                        Db.P("@Venue", form.Venue), Db.P("@StartTime", start), Db.P("@EndTime", end))))
                {
                    Flash("Tutorial created. Its register was generated from every enrolled student.");
                    return RedirectToAction("Register", new { id });
                }
            }

            form.Assignments = Assignments();
            ViewBag.Form = form;
            ViewBag.OpenForm = true;
            return View("Tutorials", Db.List<Tutorial>("sp_Tutor_Tutorials", Db.P("@TutorId", CurrentUserId)));
        }

        private Tutorial OwnTutorial(int id)
        {
            var t = Db.Single<Tutorial>("sp_Tutorial_Get", Db.P("@TutorialId", id), Db.P("@StudentId", null));
            return t != null && t.TutorId == CurrentUserId ? t : null;
        }

        public ActionResult Register(int id)
        {
            var tutorial = OwnTutorial(id);
            if (tutorial == null) return HttpNotFound();
            return View(new RegisterSheetViewModel
            {
                Tutorial = tutorial,
                Students = Db.List<RegisterEntry>("sp_Tutorial_Register", Db.P("@TutorialId", id))
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkAttendance(int tutorialId, int studentId, string status)
        {
            if (!AttendanceStatuses.Contains(status)) return JsonError("Unknown status.");
            try
            {
                Db.Exec("sp_Tutorial_MarkAttendance", Db.P("@TutorialId", tutorialId), Db.P("@StudentId", studentId),
                    Db.P("@Status", status), Db.P("@TutorId", CurrentUserId));
                return Json(new { ok = true, status });
            }
            catch (Exception ex)
            {
                string message;
                if (Db.IsUserError(ex, out message)) return JsonError(message);
                AppLog.Error("MarkAttendance", ex);
                return JsonError("Couldn't save. Check your connection and try again.", 500);
            }
        }

        public ActionResult QrCode(int id)
        {
            var tutorial = OwnTutorial(id);
            if (tutorial == null) return HttpNotFound();
            // Phones can't open "localhost" (on a phone it means the phone itself), so the QR code uses this
            // computer's network address, or SiteBaseUrl when one is configured.
            var baseUrl = NetworkAddress.PhoneBaseUrl(Request.Url, Request.ApplicationPath);
            ViewBag.CheckInUrl = baseUrl + Url.Action("CheckIn", "Student", new { token = tutorial.QrToken.ToString() });
            ViewBag.CodeUrl = baseUrl + Url.Action("Tutorials", "Student");
            ViewBag.PhoneBase = baseUrl;
            var host = new Uri(baseUrl).Host;
            ViewBag.PhoneReady = !NetworkAddress.IsLocalHost(host) && NetworkAddress.IsReachable(baseUrl);
            return View(tutorial);
        }

        /// <summary>Polled by the QR screen and register so counts update as students scan.</summary>
        public ActionResult LiveCount(int id)
        {
            var tutorial = OwnTutorial(id);
            if (tutorial == null) return HttpNotFound();
            var students = Db.List<RegisterEntry>("sp_Tutorial_Register", Db.P("@TutorialId", id));
            return Json(new
            {
                enrolled = tutorial.EnrolledCount,
                present = tutorial.PresentCount,
                late = tutorial.LateCount,
                absent = tutorial.AbsentCount,
                students = students.Select(s => new { id = s.StudentId, status = s.Status, method = s.Method })
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Change a tutorial's details. Enrolled students are notified of the change.</summary>
        [HttpGet]
        public ActionResult EditTutorial(int id)
        {
            var t = OwnTutorial(id);
            if (t == null) return HttpNotFound();
            ViewBag.Tutorial = t;
            return View(new TutorialFormViewModel
            {
                Assignment = t.ModuleId + "|" + t.YearOfStudy,
                Title = t.Title, Description = t.Description, Venue = t.Venue,
                Date = t.StartTime.Date, StartTime = t.StartTime.TimeOfDay, EndTime = t.EndTime.TimeOfDay,
                Assignments = Assignments()
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult EditTutorial(int id, TutorialFormViewModel form)
        {
            var t = OwnTutorial(id);
            if (t == null) return HttpNotFound();
            int moduleId, year;
            if (!ParseAssignment(form.Assignment, out moduleId, out year)) ModelState.AddModelError("Assignment", "Choose a module and year.");
            if (form.StartTime.HasValue && form.EndTime.HasValue && form.EndTime <= form.StartTime)
                ModelState.AddModelError("EndTime", "The end time must be after the start time.");

            if (ModelState.IsValid)
            {
                var start = form.Date.Value.Date + form.StartTime.Value;
                var end = form.Date.Value.Date + form.EndTime.Value;
                if (TryDbForm(() => Db.Exec("sp_Tutorial_Update", Db.P("@TutorialId", id), Db.P("@TutorId", CurrentUserId),
                        Db.P("@ModuleId", moduleId), Db.P("@YearOfStudy", year), Db.P("@Title", form.Title.Trim()),
                        Db.P("@Description", form.Description), Db.P("@Venue", form.Venue), Db.P("@StartTime", start), Db.P("@EndTime", end))))
                {
                    Flash("Tutorial updated. Students on the register were notified of the change.");
                    return RedirectToAction("Tutorials");
                }
            }
            form.Assignments = Assignments();
            ViewBag.Tutorial = t;
            return View(form);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteTutorial(int id)
        {
            TryDb(() => Db.Exec("sp_Tutorial_Delete", Db.P("@TutorialId", id), Db.P("@TutorId", CurrentUserId)),
                "Tutorial deleted. Students on the register were told it's cancelled.");
            return RedirectToAction("Tutorials");
        }

        // ---------- Ad-hoc class registers ----------
        public ActionResult Registers()
        {
            ViewBag.Assignments = Assignments();
            return View(Db.List<ClassRegister>("sp_ClassRegister_List", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateRegister(string assignment, string title, DateTime? registerDate)
        {
            int moduleId, year;
            if (!ParseAssignment(assignment, out moduleId, out year) || string.IsNullOrWhiteSpace(title))
            {
                FlashError("Choose a module and give the register a title.");
                return RedirectToAction("Registers");
            }
            var id = 0;
            if (TryDb(() => id = Db.ScalarInt("sp_ClassRegister_Create", Db.P("@TutorId", CurrentUserId), Db.P("@ModuleId", moduleId),
                    Db.P("@YearOfStudy", year), Db.P("@Title", title.Trim()), Db.P("@RegisterDate", (registerDate ?? TutorConnect.Helpers.Clock.Today).Date))))
                return RedirectToAction("ClassRegister", new { id });
            return RedirectToAction("Registers");
        }

        public ActionResult ClassRegister(int id)
        {
            var register = Db.Single<ClassRegister>("sp_ClassRegister_Get", Db.P("@RegisterId", id));
            if (register == null || register.TutorId != CurrentUserId) return HttpNotFound();
            return View(new RegisterSheetViewModel
            {
                ClassRegister = register,
                Students = Db.List<RegisterEntry>("sp_ClassRegister_Students", Db.P("@RegisterId", id))
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkClassRegister(int registerId, int studentId, string status)
        {
            if (!AttendanceStatuses.Contains(status)) return JsonError("Unknown status.");
            try
            {
                Db.Exec("sp_ClassRegister_Mark", Db.P("@RegisterId", registerId), Db.P("@StudentId", studentId),
                    Db.P("@Status", status), Db.P("@TutorId", CurrentUserId));
                return Json(new { ok = true, status });
            }
            catch (Exception ex)
            {
                string message;
                if (Db.IsUserError(ex, out message)) return JsonError(message);
                AppLog.Error("MarkClassRegister", ex);
                return JsonError("Couldn't save. Check your connection and try again.", 500);
            }
        }

        // ---------- Course content ----------
        [ActionName("Content")]
        public ActionResult CourseContent()
        {
            ViewBag.Assignments = Assignments();
            return View(Db.List<ContentItem>("sp_Content_ForTutor", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UploadContent(string assignment, string title, string description, HttpPostedFileBase file)
        {
            int moduleId, year;
            if (!ParseAssignment(assignment, out moduleId, out year) || string.IsNullOrWhiteSpace(title))
            {
                FlashError("Choose a module and give the material a title.");
                return RedirectToAction("Content");
            }
            string error;
            var path = FileStorage.Save(file, "content", FileStorage.ContentExtensions, 10, out error);
            if (path == null)
            {
                FlashError(error);
                return RedirectToAction("Content");
            }
            TryDb(() => Db.Exec("sp_Content_Add", Db.P("@ModuleId", moduleId), Db.P("@YearOfStudy", year), Db.P("@Title", title.Trim()),
                    Db.P("@Description", description), Db.P("@FileName", System.IO.Path.GetFileName(file.FileName)), Db.P("@FilePath", path),
                    Db.P("@UploadedBy", CurrentUserId), Db.P("@UploaderRole", "Tutor")),
                "Uploaded. The module's lecturer will review it.");
            return RedirectToAction("Content");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReviewContent(int id)
        {
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Content_MarkReviewed", Db.P("@ContentId", id), Db.P("@UserId", CurrentUserId), Db.P("@Role", "Tutor"))))
            {
                if (affected == 1) Flash("Marked as reviewed.");
                else FlashError("That item was already reviewed or isn't yours to review.");
            }
            return RedirectToAction("Content");
        }

        // ---------- Announcements ----------
        public ActionResult Announcements()
        {
            ViewBag.Assignments = Assignments();
            return View(Db.List<Announcement>("sp_Tutor_Announcements", Db.P("@TutorId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Announcements(string assignment, string title, string message)
        {
            int moduleId, year;
            if (!ParseAssignment(assignment, out moduleId, out year) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                FlashError("Choose a module, then add a title and a message.");
                return RedirectToAction("Announcements");
            }

            DataTable recipients = null;
            if (TryDb(() => recipients = Db.Query("sp_Announcement_Create", Db.P("@TutorId", CurrentUserId), Db.P("@ModuleId", moduleId),
                    Db.P("@YearOfStudy", year), Db.P("@Title", title.Trim()), Db.P("@Message", message.Trim()))))
            {
                var body = "<p><strong>" + EmailService.Encode(CurrentName) + "</strong> posted an announcement:</p>" +
                           "<p style=\"white-space:pre-line\">" + EmailService.Encode(message.Trim()) + "</p>";
                foreach (DataRow r in recipients.Rows)
                    EmailService.SendInBackground(Convert.ToString(r["Email"]), title.Trim(), "<p>Hi " + EmailService.Encode(Convert.ToString(r["FirstName"])) + ",</p>" + body);
                Flash("Announcement sent to every student in that module and year.");
            }
            return RedirectToAction("Announcements");
        }

        // ---------- Reports ----------
        public ActionResult Reports()
        {
            var ds = Db.QuerySet("sp_Tutor_Report", Db.P("@TutorId", CurrentUserId));
            ViewBag.Summary = Db.Row(ds.Tables[0]);
            ViewBag.ByModule = Db.Map<ModuleSummary>(ds.Tables[1]);
            return View(Db.Map<Booking>(ds.Tables[2]));
        }

        // ---------- Banking ----------
        [HttpGet]
        public ActionResult Banking()
        {
            var existing = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", CurrentUserId));
            var model = new BankingViewModel { Existing = existing };
            if (existing != null)
            {
                model.BankName = existing.BankName;
                model.AccountHolder = existing.AccountHolder;
                model.AccountType = existing.AccountType;
                model.BranchCode = existing.BranchCode;
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Banking(BankingViewModel model)
        {
            model.Existing = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", CurrentUserId));
            // the account number must have exactly the length the chosen bank uses
            if (!string.IsNullOrWhiteSpace(model.AccountNumber))
            {
                var problem = SouthAfricanBanks.AccountNumberProblem(SouthAfricanBanks.FindByName(model.BankName), model.AccountNumber);
                if (problem != null) ModelState.AddModelError("AccountNumber", problem);
                else model.AccountNumber = SouthAfricanBanks.Digits(model.AccountNumber);
                if (ModelState.ContainsKey("AccountNumber") && problem == null) ModelState["AccountNumber"].Errors.Clear();
            }
            if (!ModelState.IsValid) return View(model);

            var number = SouthAfricanBanks.Digits(model.AccountNumber);
            if (!TryDbForm(() => Db.Exec("sp_Banking_Save", Db.P("@TutorId", CurrentUserId), Db.P("@BankName", model.BankName),
                    Db.P("@AccountHolder", model.AccountHolder.Trim()), Db.P("@AccountType", model.AccountType), Db.P("@BranchCode", model.BranchCode),
                    Db.P("@AccountNumberProtected", SecretProtector.Protect(number)), Db.P("@AccountLast4", number.Substring(number.Length - 4)))))
                return View(model);

            var sent = 0;
            try { sent = PayoutService.ProcessPendingForTutor(CurrentUserId); }
            catch (Exception ex) { AppLog.Error("ProcessPendingForTutor", ex); }

            Flash(sent > 0
                ? "Banking details saved. " + sent + (sent == 1 ? " waiting payout was" : " waiting payouts were") + " sent straight away."
                : "Banking details saved. Future payments will be paid out to this account automatically.");
            return RedirectToAction("Banking");
        }
    }
}
