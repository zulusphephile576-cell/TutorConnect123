using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    /// <summary>Public: any student can apply to tutor and check the status of their application.</summary>
    public class TutorApplicationController : BaseController
    {
        [HttpGet]
        public ActionResult Apply()
        {
            var model = new ApplicationFormViewModel();
            User u = null;
            if (CurrentRole == "Student")
            {
                try { u = Db.Single<User>("sp_User_GetById", Db.P("@UserId", CurrentUserId)); }
                catch (System.Exception ex) { AppLog.Error("TutorApplication.Apply prefill", ex); }
            }
            if (u != null)
            {
                model.FirstName = u.FirstName;
                model.LastName = u.LastName;
                model.StudentNumber = u.StudentNumber;
                model.Email = u.Email;
                model.Phone = u.Phone;
                model.CourseId = u.CourseId;
                model.CurrentYear = u.YearOfStudy;
            }
            Fill(model);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Apply(ApplicationFormViewModel model)
        {
            Fill(model);
            foreach (var e in CourseCatalog.Validate(model.FacultyId, model.DepartmentId, model.CourseId, model.Faculties, model.Departments, model.Courses))
                if (!ModelState.ContainsKey(e.Key) || ModelState[e.Key].Errors.Count == 0) ModelState.AddModelError(e.Key, e.Value);
            if (model.IdDocument == null || model.IdDocument.ContentLength == 0)
                ModelState.AddModelError("IdDocument", "Upload a copy of your student card.");
            if (model.Transcript == null || model.Transcript.ContentLength == 0)
                ModelState.AddModelError("Transcript", "Upload your academic transcript. It's required so your mark can be checked.");
            if (model.Cv == null || model.Cv.ContentLength == 0)
                ModelState.AddModelError("Cv", "Upload your CV (PDF or Word).");
            if (!ModelState.IsValid) return View(model);

            string error;
            var idPath = FileStorage.Save(model.IdDocument, "applications", FileStorage.DocumentExtensions, 10, out error);
            if (idPath == null)
            {
                ModelState.AddModelError("IdDocument", error);
                return View(model);
            }

            string transcriptPath;
            {
                transcriptPath = FileStorage.Save(model.Transcript, "applications", FileStorage.DocumentExtensions, 10, out error);
                if (transcriptPath == null)
                {
                    ModelState.AddModelError("Transcript", error);
                    return View(model);
                }
            }

            var cvPath = FileStorage.Save(model.Cv, "applications", FileStorage.CvExtensions, 10, out error);
            if (cvPath == null)
            {
                ModelState.AddModelError("Cv", error);
                return View(model);
            }

            if (!TryDbForm(() => Db.ScalarInt("sp_Application_Create",
                    Db.P("@FirstName", model.FirstName.Trim()), Db.P("@LastName", model.LastName.Trim()), Db.P("@StudentNumber", model.StudentNumber.Trim()),
                    Db.P("@Email", model.Email.Trim()), Db.P("@PersonalEmail", model.PersonalEmail.Trim()), Db.P("@Phone", model.Phone),
                    Db.P("@CourseId", model.CourseId), Db.P("@CurrentYear", model.CurrentYear), Db.P("@ModuleId", model.ModuleId),
                    Db.P("@TutorYear", model.TutorYear), Db.P("@ModuleMark", model.ModuleMark), Db.P("@Motivation", model.Motivation.Trim()),
                    Db.P("@IdDocumentPath", idPath), Db.P("@TranscriptPath", transcriptPath), Db.P("@CvPath", cvPath))))
                return View(model);

            EmailService.SendInBackground(model.PersonalEmail.Trim(), "We received your tutor application",
                "<p>Hi " + EmailService.Encode(model.FirstName) + ",</p><p>Thanks for applying to tutor. A lecturer will review your application and an administrator makes the final decision. " +
                "You can check progress at any time on the <a href=\"" + AppConfig.BaseUrl + "/TutorApplication/Status\" style=\"color:#4F46E5\">application status page</a>.</p>");

            Flash("Application submitted. You can check its progress here at any time.");
            return RedirectToAction("Status", new { email = model.Email.Trim() });
        }

        [HttpGet]
        public ActionResult Status(string email)
        {
            if (string.IsNullOrWhiteSpace(email) && CurrentRole == "Student") email = Session["Email"] as string;
            ViewBag.Email = email;
            if (string.IsNullOrWhiteSpace(email)) return View(new System.Collections.Generic.List<TutorApplication>());
            try
            {
                return View(Db.List<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", null), Db.P("@Email", email.Trim()), Db.P("@LecturerId", null)));
            }
            catch (System.Exception ex)
            {
                AppLog.Error("TutorApplication.Status", ex);
                FlashError("Applications couldn't be loaded: " + ex.GetBaseException().Message);
                return View(new System.Collections.Generic.List<TutorApplication>());
            }
        }

        /// <summary>Loads the course and module lists. A database problem becomes a message on the form, not a crash.</summary>
        private void Fill(ApplicationFormViewModel model)
        {
            try
            {
                var catalog = CourseCatalog.Load();
                model.Faculties = catalog.Faculties;
                model.Departments = catalog.Departments;
                model.Courses = catalog.Courses;
                model.Modules = Db.List<Module>("sp_Module_List");
                if (!model.FacultyId.HasValue && model.CourseId.HasValue)
                {
                    int? f, d;
                    catalog.Locate(model.CourseId, out f, out d);
                    model.FacultyId = f;
                    model.DepartmentId = d;
                }
            }
            catch (System.Exception ex)
            {
                AppLog.Error("TutorApplication.Fill", ex);
                ModelState.AddModelError("", "The course and module lists couldn't be loaded: " + ex.GetBaseException().Message);
            }
        }
    }
}
