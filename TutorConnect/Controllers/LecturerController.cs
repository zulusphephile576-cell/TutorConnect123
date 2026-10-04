using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    [RoleAuthorize("Lecturer")]
    public class LecturerController : BaseController
    {
        public ActionResult Dashboard()
        {
            var ds = Db.QuerySet("sp_Lecturer_Dashboard", Db.P("@LecturerId", CurrentUserId));
            ViewBag.Modules = Db.List<Module>("sp_Lecturer_Modules", Db.P("@LecturerId", CurrentUserId));
            return View(new DashboardViewModel
            {
                Stats = Db.Row(ds.Tables[0]),
                Activity = Db.Map<ActivityItem>(ds.Tables[1])
            });
        }

        // ---------- Tutor assignments ----------
        /// <summary>The modules this lecturer teaches, with their tutors, students, content and attendance.</summary>
        public ActionResult Modules()
        {
            var ds = Db.QuerySet("sp_Lecturer_ModuleOverview", Db.P("@LecturerId", CurrentUserId));
            ViewBag.Tutors = ds.Tables.Count > 1 ? Db.Map<ModuleTutor>(ds.Tables[1]) : new List<ModuleTutor>();
            return View(Db.Map<ModuleOverview>(ds.Tables[0]));
        }

        public ActionResult AssignTutors()
        {
            ViewBag.Modules = Db.List<Module>("sp_Lecturer_Modules", Db.P("@LecturerId", CurrentUserId));
            ViewBag.Tutors = Db.List<TutorPoolEntry>("sp_Lecturer_TutorPool", Db.P("@LecturerId", CurrentUserId));
            return View(Db.List<TutorAssignment>("sp_Lecturer_Assignments", Db.P("@LecturerId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Assign(int? tutorId, int? moduleId, int? yearOfStudy)
        {
            if (!tutorId.HasValue || !moduleId.HasValue || !yearOfStudy.HasValue)
            {
                FlashError("Choose a tutor, a module and a year of study.");
                return RedirectToAction("AssignTutors");
            }
            TryDb(() => Db.Exec("sp_TutorModule_Assign", Db.P("@TutorId", tutorId), Db.P("@ModuleId", moduleId), Db.P("@YearOfStudy", yearOfStudy),
                    Db.P("@AssignedBy", CurrentUserId), Db.P("@AssignedByRole", "Lecturer")),
                "Tutor assigned.");
            return RedirectToAction("AssignTutors");
        }

        /// <summary>How many tutors a module needs. Assigning stops once this number is reached.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetTutorsNeeded(int moduleId, int? needed)
        {
            if (!needed.HasValue) FlashError("Enter how many tutors this module needs.");
            else TryDb(() => Db.Exec("sp_Module_SetTutorsNeeded", Db.P("@ModuleId", moduleId), Db.P("@LecturerId", CurrentUserId), Db.P("@Needed", needed.Value)),
                "Saved. The administrator can see how many tutors you need.");
            return Redirect(Url.Action("AssignTutors") + "#needs");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RemoveAssignment(int id)
        {
            TryDb(() => Db.Exec("sp_TutorModule_Remove", Db.P("@TutorModuleId", id), Db.P("@LecturerId", CurrentUserId)), "Assignment removed.");
            return RedirectToAction("AssignTutors");
        }

        // ---------- Course content ----------
        [ActionName("Content")]
        public ActionResult CourseContent()
        {
            ViewBag.Modules = Db.List<Module>("sp_Lecturer_Modules", Db.P("@LecturerId", CurrentUserId));
            return View(Db.List<ContentItem>("sp_Lecturer_Content", Db.P("@LecturerId", CurrentUserId)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UploadContent(int? moduleId, int? yearOfStudy, string title, string description, HttpPostedFileBase file)
        {
            if (!moduleId.HasValue || string.IsNullOrWhiteSpace(title))
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
            TryDb(() => Db.Exec("sp_Content_Add", Db.P("@ModuleId", moduleId), Db.P("@YearOfStudy", yearOfStudy), Db.P("@Title", title.Trim()),
                    Db.P("@Description", description), Db.P("@FileName", System.IO.Path.GetFileName(file.FileName)), Db.P("@FilePath", path),
                    Db.P("@UploadedBy", CurrentUserId), Db.P("@UploaderRole", "Lecturer")),
                "Uploaded. Tutors on this module will see it in their content list.");
            return RedirectToAction("Content");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReviewContent(int id)
        {
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Content_MarkReviewed", Db.P("@ContentId", id), Db.P("@UserId", CurrentUserId), Db.P("@Role", "Lecturer"))))
            {
                if (affected == 1) Flash("Marked as reviewed.");
                else FlashError("That item was already reviewed or isn't yours to review.");
            }
            return RedirectToAction("Content");
        }

        // ---------- Oversight ----------
        public ActionResult Attendance()
        {
            return View(Db.List<AttendanceRecord>("sp_Attendance_Records", Db.P("@LecturerId", CurrentUserId)));
        }

        public ActionResult Progress()
        {
            var ds = Db.QuerySet("sp_Lecturer_Progress", Db.P("@LecturerId", CurrentUserId));
            ViewBag.Tutors = Db.Map<TutorActivity>(ds.Tables[1]);
            return View(Db.Map<StudentProgress>(ds.Tables[0]));
        }

        public ActionResult Tutorials()
        {
            return View(Db.List<Tutorial>("sp_Lecturer_Tutorials", Db.P("@LecturerId", CurrentUserId)));
        }

        // ---------- Tutor applications ----------
        public ActionResult Applications()
        {
            return View(Db.List<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", null), Db.P("@Email", null), Db.P("@LecturerId", CurrentUserId)));
        }

        public ActionResult Application(int id)
        {
            var app = Db.Single<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", id), Db.P("@Email", null), Db.P("@LecturerId", CurrentUserId));
            if (app == null) return HttpNotFound();
            ViewBag.Notes = LoadNotes(id);
            return View(app);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Recommend(int id, string recommendation, string comment)
        {
            if (recommendation != "Approve" && recommendation != "Reject")
            {
                FlashError("Choose whether you recommend approving or rejecting.");
                return RedirectToAction("Application", new { id });
            }
            if (string.IsNullOrWhiteSpace(comment))
            {
                FlashError("Add a short comment so the administrator understands your recommendation.");
                return RedirectToAction("Application", new { id });
            }
            var affected = 0;
            if (TryDb(() => affected = Db.ScalarInt("sp_Application_Recommend", Db.P("@ApplicationId", id), Db.P("@LecturerId", CurrentUserId),
                    Db.P("@Recommendation", recommendation), Db.P("@Comment", comment.Trim()))))
            {
                if (affected == 1) Flash("Recommendation sent to the administrator, who makes the final decision.");
                else FlashError("This application is no longer open for your review.");
            }
            return RedirectToAction("Applications");
        }
    
        // ---------- Notes between the administrator and the reviewing lecturer ----------
        private List<ApplicationComment> LoadNotes(int applicationId)
        {
            try { return Db.List<ApplicationComment>("sp_ApplicationComment_List", Db.P("@ApplicationId", applicationId)); }
            catch (Exception ex) { AppLog.Error("LoadNotes", ex); return new List<ApplicationComment>(); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddNote(int id, string comment)
        {
            comment = (comment ?? "").Trim();
            if (comment.Length == 0) FlashError("Write something first.");
            else if (comment.Length > 1000) FlashError("Keep notes under 1 000 characters.");
            else TryDb(() => Db.Exec("sp_ApplicationComment_Add", Db.P("@ApplicationId", id), Db.P("@UserId", CurrentUserId),
                    Db.P("@Role", CurrentRole), Db.P("@Comment", comment)), "Note sent.");
            return Redirect(Url.Action("Application", new { id }) + "#notes");
        }
    }
}
