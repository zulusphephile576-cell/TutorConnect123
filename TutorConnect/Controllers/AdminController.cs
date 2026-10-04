using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;
using TutorConnect.Services.Payouts;

namespace TutorConnect.Controllers
{
    [RoleAuthorize("Admin")]
    public class AdminController : BaseController
    {
        public ActionResult Dashboard()
        {
            var ds = Db.QuerySet("sp_Admin_Dashboard");
            return View(new DashboardViewModel
            {
                Stats = Db.Row(ds.Tables[0]),
                Activity = Db.Map<ActivityItem>(ds.Tables[1]),
                Applications = Db.Map<TutorApplication>(ds.Tables[2])
            });
        }

        // ---------- Users ----------
        public ActionResult Users(string role)
        {
            ViewBag.Role = role;
            ViewBag.Catalog = CourseCatalog.Load();
            return View(Db.List<User>("sp_User_List", Db.P("@Role", string.IsNullOrEmpty(role) ? null : role)));
        }

        /// <summary>Everything TutorConnect holds about one user, on one page (read-only).</summary>
        public ActionResult UserDetails(int id)
        {
            var user = Db.Single<User>("sp_User_GetById", Db.P("@UserId", id));
            if (user == null) return HttpNotFound();
            var summary = Db.List<User>("sp_User_List", Db.P("@Role", user.Role)).FirstOrDefault(x => x.UserId == id);
            ViewBag.Summary = summary;
            ViewBag.Data = PrivacyService.Export(id);
            if (user.Role == "Tutor")
            {
                ViewBag.Assignments = Db.List<TutorAssignment>("sp_Tutor_Assignments", Db.P("@TutorId", id));
                ViewBag.Banking = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", id));
            }
            if (user.Role == "Lecturer")
                ViewBag.LecturerModules = Db.List<Module>("sp_Lecturer_Modules", Db.P("@LecturerId", id));
            return View(user);
        }

        [HttpGet]
        public ActionResult EditUser(int id)
        {
            var user = Db.Single<User>("sp_User_GetById", Db.P("@UserId", id));
            if (user == null) return HttpNotFound();
            ViewBag.Courses = Db.List<Course>("sp_Course_List");
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult EditUser([Bind(Include = "UserId,FirstName,LastName,Email,StudentNumber,Phone,CourseId,YearOfStudy,PersonalEmail,IsActive")] User model)
        {
            var existing = Db.Single<User>("sp_User_GetById", Db.P("@UserId", model.UserId));
            if (existing == null) return HttpNotFound();
            if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName) || string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError("", "First name, last name and email are required.");
            if (existing.Role == "Student" && (!model.CourseId.HasValue || !model.YearOfStudy.HasValue))
                ModelState.AddModelError("", "Students need a course and a year of study.");
            if (model.UserId == CurrentUserId && !model.IsActive)
                ModelState.AddModelError("", "You can't deactivate your own account.");

            if (ModelState.IsValid && TryDbForm(() => Db.Exec("sp_User_Update", Db.P("@UserId", model.UserId), Db.P("@FirstName", model.FirstName.Trim()),
                    Db.P("@LastName", model.LastName.Trim()), Db.P("@Email", model.Email.Trim()), Db.P("@StudentNumber", model.StudentNumber),
                    Db.P("@Phone", model.Phone), Db.P("@CourseId", model.CourseId), Db.P("@YearOfStudy", model.YearOfStudy),
                    Db.P("@PersonalEmail", model.PersonalEmail), Db.P("@IsActive", model.IsActive))))
            {
                Flash(model.FirstName + "'s account was updated.");
                return RedirectToAction("Users", new { role = existing.Role });
            }

            model.Role = existing.Role;
            model.CreatedAt = existing.CreatedAt;
            ViewBag.Courses = Db.List<Course>("sp_Course_List");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteUser(int id, string returnRole)
        {
            if (id == CurrentUserId)
            {
                FlashError("You can't delete your own account.");
                return RedirectToAction("Users", new { role = returnRole });
            }
            var result = "";
            if (TryDb(() => result = Convert.ToString(Db.Scalar("sp_User_Delete", Db.P("@UserId", id)))))
            {
                Flash(result == "Deactivated"
                    ? "This account has payment history, so it was deactivated instead of deleted. Records stay intact."
                    : "The account was deleted.");
            }
            return RedirectToAction("Users", new { role = returnRole });
        }

        /// <summary>Clears a sign-in lockout (after 5 failed attempts) before the 15 minutes are up.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UnlockUser(int id, string returnRole)
        {
            TryDb(() => Db.Exec("sp_User_LoginSucceeded", Db.P("@UserId", id)), "Account unlocked. They can sign in again now.");
            return RedirectToAction("Users", new { role = returnRole });
        }

        // ---------- Email settings and test ----------
        [HttpGet]
        public ActionResult Email()
        {
            ViewBag.Enabled = EmailService.Enabled;
            ViewBag.Host = EmailService.Host;
            ViewBag.Port = EmailService.Port;
            ViewBag.User = EmailService.User;
            ViewBag.From = EmailService.From;
            ViewBag.Problems = EmailService.ConfigProblems();
            ViewBag.HasPassword = !string.IsNullOrWhiteSpace(AppConfig.Get("Smtp:Pass")) && AppConfig.Get("Smtp:Pass") != "your-16-character-app-password";
            ViewBag.LastError = EmailService.LastError;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SendTestEmail(string to)
        {
            to = (to ?? "").Trim();
            if (to.Length == 0) { FlashError("Enter an address to send the test to."); return RedirectToAction("Email"); }
            var ok = EmailService.Send(to, "TutorConnect test email",
                "<p>This is a test from TutorConnect. If you can read this, email is working and applicants will receive their decisions and login details.</p>");
            if (!EmailService.Enabled) Flash("Email is switched off, so the test was written to App_Data/emails.log instead. Set Smtp:Enabled to true in Web.config to send real email.", "info");
            else if (ok) Flash("Test email sent to " + to + ". Check the inbox (and the spam folder).");
            else FlashError("The test email failed. " + EmailService.LastError);
            return RedirectToAction("Email");
        }

        // ---------- Lecturers ----------
        [HttpGet]
        public ActionResult CreateLecturer()
        {
            return View(new CreateLecturerViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateLecturer(CreateLecturerViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var password = PasswordHasher.GenerateTemporary();
            if (!TryDbForm(() => Db.ScalarInt("sp_Lecturer_Create", Db.P("@FirstName", model.FirstName.Trim()), Db.P("@LastName", model.LastName.Trim()),
                    Db.P("@Email", model.Email.Trim()), Db.P("@PersonalEmail", model.PersonalEmail.Trim()), Db.P("@Phone", model.Phone),
                    Db.P("@PasswordHash", PasswordHasher.Hash(password)))))
                return View(model);

            var sent = EmailService.Send(model.PersonalEmail.Trim(), "Your TutorConnect lecturer account",
                LoginDetailsBody(model.FirstName, "A lecturer account has been created for you.", model.Email.Trim(), password, "Lecturer"));

            if (sent && AppConfig.GetBool("Smtp:Enabled"))
                Flash("Lecturer account created. Login details were emailed to " + model.PersonalEmail.Trim() + ".");
            else
                Flash("Lecturer account created. Email is off, so share this temporary password securely: " + password, "info");
            return RedirectToAction("Users", new { role = "Lecturer" });
        }

        private static string LoginDetailsBody(string firstName, string intro, string loginEmail, string password, string role)
        {
            return "<p>Hi " + EmailService.Encode(firstName) + ",</p><p>" + intro + "</p>" +
                   "<table cellpadding=\"6\" style=\"background:#F4F4FB;border-radius:8px;margin:12px 0\">" +
                   "<tr><td>Sign in as</td><td><strong>" + role + "</strong></td></tr>" +
                   "<tr><td>Email</td><td><strong>" + EmailService.Encode(loginEmail) + "</strong></td></tr>" +
                   "<tr><td>Temporary password</td><td><strong>" + EmailService.Encode(password) + "</strong></td></tr></table>" +
                   "<p>Please change your password under Settings after you sign in.</p>" +
                   "<p><a href=\"" + AppConfig.BaseUrl + "/Account/Login?role=" + role + "\" style=\"color:#4F46E5\">Sign in to TutorConnect</a></p>";
        }

        // ---------- Tutor applications ----------
        public ActionResult Applications(string status)
        {
            ViewBag.Status = status;
            var apps = Db.List<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", null), Db.P("@Email", null), Db.P("@LecturerId", null));
            if (status == "open") apps = apps.Where(a => !a.IsDecided).ToList();
            else if (status == "decided") apps = apps.Where(a => a.IsDecided).ToList();
            return View(apps);
        }

        public ActionResult Application(int id)
        {
            var app = Db.Single<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", id), Db.P("@Email", null), Db.P("@LecturerId", null));
            if (app == null) return HttpNotFound();
            ViewBag.Lecturers = Db.List<User>("sp_Lecturer_List");
            ViewBag.Notes = LoadNotes(id);
            return View(app);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AssignLecturer(int id, int? lecturerId)
        {
            if (!lecturerId.HasValue) FlashError("Choose a lecturer.");
            else if (TryDb(() => Db.Exec("sp_Application_AssignLecturer", Db.P("@ApplicationId", id), Db.P("@LecturerId", lecturerId))))
            {
                var lecturer = Db.Single<User>("sp_User_GetById", Db.P("@UserId", lecturerId.Value));
                var app = Db.Single<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", id), Db.P("@Email", null), Db.P("@LecturerId", null));
                var sent = lecturer != null && app != null && EmailService.Send(lecturer.Email, "Tutor application to review: " + app.FullName,
                    "<p>Hi " + EmailService.Encode(lecturer.FirstName) + ",</p><p>Please review the tutor application from <b>" + EmailService.Encode(app.FullName) +
                    "</b> for <b>" + EmailService.Encode(app.ModuleCode) + "</b>. You can add notes for the administrator and give your recommendation.</p>" +
                    "<p><a href=\"" + AppConfig.BaseUrl + "/Lecturer/Application/" + id + "\" style=\"color:#4F46E5\">Open the application</a></p>");
                Flash("Lecturer assigned. " + (sent && EmailService.Enabled ? "They were emailed and will see it in their review list." : "They'll see it in their review list (email is switched off, so no email was sent)."));
            }
            return RedirectToAction("Application", new { id });
        }

        /// <summary>
        /// Sends the decision email again (for example after fixing the SMTP settings). For approvals a new temporary
        /// password is issued, because passwords are stored only as hashes and the old one can't be recovered.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResendDecision(int id)
        {
            var app = Db.Single<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", id), Db.P("@Email", null), Db.P("@LecturerId", null));
            if (app == null) return HttpNotFound();
            if (!app.IsDecided) { FlashError("This application hasn't been decided yet."); return RedirectToAction("Application", new { id }); }
            if (string.IsNullOrWhiteSpace(app.PersonalEmail)) { FlashError("The applicant has no personal email address on file."); return RedirectToAction("Application", new { id }); }

            var feedbackHtml = string.IsNullOrWhiteSpace(app.AdminFeedback) ? "" : "<p><em>" + EmailService.Encode(app.AdminFeedback) + "</em></p>";
            bool sent;
            string password = null;
            if (app.Status == "Approved")
            {
                var tutor = Db.Single<User>("sp_User_GetByEmailRole", Db.P("@Email", app.Email), Db.P("@Role", "Tutor"));
                if (tutor == null) { FlashError("The tutor account for " + app.Email + " couldn't be found."); return RedirectToAction("Application", new { id }); }
                password = PasswordHasher.GenerateTemporary();
                if (!TryDb(() => Db.Exec("sp_User_ChangePassword", Db.P("@UserId", tutor.UserId), Db.P("@PasswordHash", PasswordHasher.Hash(password)))))
                    return RedirectToAction("Application", new { id });
                sent = EmailService.Send(app.PersonalEmail, "Your tutor application was approved",
                    LoginDetailsBody(app.FirstName, "Congratulations, your application to tutor was approved and your tutor account is ready. (This email replaces any earlier one.)" + feedbackHtml, app.Email, password, "Tutor"));
            }
            else
            {
                sent = EmailService.Send(app.PersonalEmail, "An update on your tutor application",
                    "<p>Hi " + EmailService.Encode(app.FirstName) + ",</p><p>Thank you for applying to tutor. Unfortunately your application wasn't successful this time.</p>" + feedbackHtml +
                    "<p>You're welcome to apply again in a future semester.</p>");
            }

            if (sent && EmailService.Enabled)
                Flash("Decision email sent to " + app.PersonalEmail + "." + (password != null ? " It contains a new temporary password." : ""));
            else
                Flash((!EmailService.Enabled ? "Email is switched off (Smtp:Enabled is false), so nothing was sent." : "The email couldn't be sent: " + EmailService.LastError)
                      + (password != null ? " A new temporary password was set; share it securely: " + password : "") + " Check the settings under Admin, Email.", "info");
            return RedirectToAction("Application", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(int id, string decision, string feedback)
        {
            feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
            if (decision == "Approve")
            {
                var password = PasswordHasher.GenerateTemporary();
                Dictionary<string, object> row = null;
                if (!TryDb(() => row = Db.Row(Db.Query("sp_Application_Approve", Db.P("@ApplicationId", id),
                        Db.P("@PasswordHash", PasswordHasher.Hash(password)), Db.P("@Feedback", feedback)))))
                    return RedirectToAction("Application", new { id });

                var created = Convert.ToBoolean(row["Created"]);
                var personal = Convert.ToString(row["PersonalEmail"]);
                var first = Convert.ToString(row["FirstName"]);
                var login = Convert.ToString(row["LoginEmail"]);
                var feedbackHtml = feedback == null ? "" : "<p><em>" + EmailService.Encode(feedback) + "</em></p>";

                bool sent;
                if (created)
                {
                    sent = EmailService.Send(personal, "Your tutor application was approved",
                        LoginDetailsBody(first, "Congratulations, your application to tutor was approved and your tutor account is ready." + feedbackHtml, login, password, "Tutor"));
                }
                else
                {
                    sent = EmailService.Send(personal, "Your tutor application was approved",
                        "<p>Hi " + EmailService.Encode(first) + ",</p><p>Your application was approved. You already have a tutor account, so sign in with your existing password on the Tutor tab.</p>" + feedbackHtml);
                }

                var delivered = sent && EmailService.Enabled;
                var why = !EmailService.Enabled ? "Email is switched off (Smtp:Enabled is false), so the message was saved to App_Data/emails.log instead of being sent."
                                                : "The email couldn't be sent: " + EmailService.LastError;
                if (delivered)
                    Flash("Approved. " + (created ? "The tutor account was created and the login details were emailed to " : "The applicant was emailed at ") + personal + ".");
                else if (created)
                    Flash("Approved and the tutor account was created. " + why + " Share this temporary password with the applicant securely: " + password + ". Test your email settings under Admin, Email.", "info");
                else
                    Flash("Approved. " + why + " Let the applicant know they can sign in with their existing tutor password.", "info");
            }
            else if (decision == "Reject")
            {
                if (feedback == null)
                {
                    FlashError("Add feedback so the applicant understands the decision.");
                    return RedirectToAction("Application", new { id });
                }
                Dictionary<string, object> row = null;
                if (!TryDb(() => row = Db.Row(Db.Query("sp_Application_Reject", Db.P("@ApplicationId", id), Db.P("@Feedback", feedback)))))
                    return RedirectToAction("Application", new { id });

                var rejectSent = EmailService.Send(Convert.ToString(row["PersonalEmail"]), "An update on your tutor application",
                    "<p>Hi " + EmailService.Encode(Convert.ToString(row["FirstName"])) + ",</p><p>Thank you for applying to tutor. Unfortunately your application wasn't successful this time.</p>" +
                    "<p><em>" + EmailService.Encode(feedback) + "</em></p><p>You're welcome to apply again in a future semester.</p>");
                if (rejectSent && EmailService.Enabled) Flash("Application rejected and the applicant was emailed.");
                else Flash("Application rejected. " + (!EmailService.Enabled ? "Email is switched off, so no email was sent." : "The email couldn't be sent: " + EmailService.LastError), "info");
            }
            else
            {
                FlashError("Choose approve or reject.");
            }
            return RedirectToAction("Application", new { id });
        }

        // ---------- Modules ----------
        public ActionResult Modules(int? edit)
        {
            var modules = Db.List<Module>("sp_Module_List");
            ViewBag.Lecturers = Db.List<User>("sp_Lecturer_List");
            ViewBag.Catalog = CourseCatalog.Load();
            ViewBag.Editing = edit.HasValue ? modules.FirstOrDefault(m => m.ModuleId == edit.Value) : null;
            return View(modules);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveModule(int? moduleId, string code, string name, int? yearLevel, string description, int? lecturerId, int? semester)
        {
            if (semester != 1 && semester != 2) { FlashError("Choose semester 1 or semester 2."); return RedirectToAction("Modules", new { edit = moduleId }); }
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || !yearLevel.HasValue || yearLevel < 1 || yearLevel > 7)
            {
                FlashError("A module needs a code, a name and a year level between 1 and 7.");
                return RedirectToAction("Modules", new { edit = moduleId });
            }
            if (TryDb(() => Db.ScalarInt("sp_Module_Save", Db.P("@ModuleId", moduleId), Db.P("@Code", code.Trim()), Db.P("@Name", name.Trim()),
                    Db.P("@YearLevel", yearLevel), Db.P("@Description", description), Db.P("@LecturerId", lecturerId), Db.P("@Semester", semester)),
                    moduleId.HasValue ? "Module updated." : "Module created. Link it to a course under Courses so students are registered."))
                return RedirectToAction("Modules");
            return RedirectToAction("Modules", new { edit = moduleId });
        }

        /// <summary>Adds any number of modules at once (one row per module, each with its year and semester).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddModules(List<ModuleRow> modules)
        {
            var rows = (modules ?? new List<ModuleRow>()).Where(r => !r.IsBlank).ToList();
            var problems = new List<string>();
            if (rows.Count == 0) problems.Add("Fill in at least one module.");
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var label = "Module " + (i + 1) + (string.IsNullOrWhiteSpace(r.Code) ? "" : " (" + r.Code.Trim().ToUpperInvariant() + ")");
                if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)) problems.Add(label + " needs both a code and a name.");
                else if (r.Code.Trim().Length > 20) problems.Add(label + ": the code can be at most 20 characters.");
                if (r.YearLevel < 1 || r.YearLevel > 7) problems.Add(label + ": choose a year between 1 and 7.");
                if (r.Semester != 1 && r.Semester != 2) problems.Add(label + ": choose semester 1 or 2.");
            }
            if (rows.Where(r => !string.IsNullOrWhiteSpace(r.Code)).GroupBy(r => r.Code.Trim().ToUpperInvariant()).Any(g => g.Count() > 1))
                problems.Add("The same module code appears more than once.");
            if (problems.Count > 0)
            {
                FlashError(string.Join(" ", problems));
                TempData["PendingModules"] = rows;   // keep what was typed
                return RedirectToAction("Modules");
            }

            var created = 0;
            if (TryDb(() => created = Db.ScalarInt("sp_Module_BulkCreate", Db.Table("@Modules", "dbo.ModuleListType", ModuleTable(rows)))))
            {
                Flash(created + (created == 1 ? " module was" : " modules were") + " added. Link them to courses under Courses so students are registered.");
                return RedirectToAction("Modules");
            }
            TempData["PendingModules"] = rows;
            return RedirectToAction("Modules");
        }

        /// <summary>Rows for the dbo.ModuleListType table parameter (Code, Name, YearLevel, Semester, LecturerId).</summary>
        private static DataTable ModuleTable(IEnumerable<ModuleRow> rows)
        {
            var table = new DataTable();
            table.Columns.Add("Code", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("YearLevel", typeof(int));
            table.Columns.Add("Semester", typeof(int));
            table.Columns.Add("LecturerId", typeof(int));
            foreach (var r in rows)
                table.Rows.Add(r.Code.Trim().ToUpperInvariant(), r.Name.Trim(), r.YearLevel, r.Semester == 2 ? 2 : 1,
                    r.LecturerId.HasValue ? (object)r.LecturerId.Value : DBNull.Value);
            return table;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteModule(int id)
        {
            TryDb(() => Db.Exec("sp_Module_Delete", Db.P("@ModuleId", id)), "Module deleted.");
            return RedirectToAction("Modules");
        }

        // ---------- Courses ----------
        public ActionResult Courses()
        {
            var ds = Db.QuerySet("sp_Course_ListWithModules");
            ViewBag.CourseModules = Db.Map<Module>(ds.Tables[1]);
            ViewBag.AllModules = Db.List<Module>("sp_Module_List");
            ViewBag.Departments = Db.List<Department>("sp_Department_List", Db.P("@FacultyId", null));
            ViewBag.Faculties = Db.List<Faculty>("sp_Faculty_List");
            return View(Db.Map<Course>(ds.Tables[0]));
        }

        [HttpGet]
        public ActionResult CreateCourse()
        {
            var model = new CourseFormViewModel();
            model.Modules.Add(new ModuleRow());   // one row to start; a new one appears as each is filled in
            FillCatalog(model);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateCourse(CourseFormViewModel model)
        {
            var rows = (model.Modules ?? new List<ModuleRow>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Code) || !string.IsNullOrWhiteSpace(r.Name)).ToList();

            var catalog = FillCatalog(model);
            foreach (var e in catalog.Validate(model.FacultyId, model.DepartmentId, null, courseRequired: false))
                if (!ModelState.ContainsKey(e.Key) || ModelState[e.Key].Errors.Count == 0) ModelState.AddModelError(e.Key, e.Value);
            if (rows.Any(r => string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)))
                ModelState.AddModelError("", "Each module row needs both a code and a name (leave unused rows completely empty).");
            if (rows.Any(r => r.YearLevel < 1 || r.YearLevel > 7)) ModelState.AddModelError("", "Module year levels must be between 1 and 7.");
            if (rows.Any(r => r.Semester != 1 && r.Semester != 2)) ModelState.AddModelError("", "Choose semester 1 or semester 2 for every module.");
            if (rows.Where(r => !string.IsNullOrWhiteSpace(r.Code)).GroupBy(r => r.Code.Trim().ToUpperInvariant()).Any(g => g.Count() > 1)) ModelState.AddModelError("", "Each module code can only appear once.");

            if (ModelState.IsValid)
            {
                var table = ModuleTable(rows);
                if (TryDbForm(() => Db.ScalarInt("sp_Course_CreateWithModules", Db.P("@Code", model.Code.Trim()), Db.P("@Name", model.Name.Trim()),
                        Db.P("@Description", model.Description), Db.Table("@Modules", "dbo.ModuleListType", table),
                        Db.P("@DepartmentId", model.DepartmentId))))
                {
                    Flash("Course created with " + rows.Count + (rows.Count == 1 ? " module." : " modules.") + " Assign a lecturer to each new module under Modules.");
                    return RedirectToAction("Courses");
                }
            }

            model.Modules = rows.Count > 0 ? rows : new List<ModuleRow> { new ModuleRow() };
            return View(model);
        }

        private static CourseCatalog FillCatalog(CourseFormViewModel model)
        {
            var catalog = CourseCatalog.Load();
            model.Faculties = catalog.Faculties;
            model.Departments = catalog.Departments;
            return catalog;
        }

        /// <summary>Moves an existing course to another department (and so, possibly, another faculty).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MoveCourse(int courseId, int? departmentId)
        {
            if (!departmentId.HasValue) FlashError("Choose a department.");
            else TryDb(() => Db.Exec("sp_Course_SetDepartment", Db.P("@CourseId", courseId), Db.P("@DepartmentId", departmentId)),
                "Course moved. Students now see it under its new faculty and department.");
            return RedirectToAction("Courses");
        }

        // ---------- Faculties and departments ----------
        public ActionResult Faculties()
        {
            ViewBag.Departments = Db.List<Department>("sp_Department_List", Db.P("@FacultyId", null));
            return View(Db.List<Faculty>("sp_Faculty_List"));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveFaculty(int? facultyId, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) FlashError("Give the faculty a name.");
            else TryDb(() => Db.ScalarInt("sp_Faculty_Save", Db.P("@FacultyId", facultyId), Db.P("@Name", name.Trim())),
                facultyId.HasValue ? "Faculty renamed." : "Faculty added. Now add its departments.");
            return RedirectToAction("Faculties");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteFaculty(int id)
        {
            TryDb(() => Db.Exec("sp_Faculty_Delete", Db.P("@FacultyId", id)), "Faculty removed.");
            return RedirectToAction("Faculties");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveDepartment(int? departmentId, int? facultyId, string name)
        {
            if (!facultyId.HasValue || string.IsNullOrWhiteSpace(name)) FlashError("Choose a faculty and give the department a name.");
            else TryDb(() => Db.ScalarInt("sp_Department_Save", Db.P("@DepartmentId", departmentId), Db.P("@FacultyId", facultyId), Db.P("@Name", name.Trim())),
                departmentId.HasValue ? "Department updated." : "Department added. You can now create courses under it.");
            return RedirectToAction("Faculties");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteDepartment(int id)
        {
            TryDb(() => Db.Exec("sp_Department_Delete", Db.P("@DepartmentId", id)), "Department removed.");
            return RedirectToAction("Faculties");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LinkModule(int courseId, int? moduleId)
        {
            if (!moduleId.HasValue) FlashError("Choose a module to link.");
            else TryDb(() => Db.Exec("sp_CourseModule_Link", Db.P("@CourseId", courseId), Db.P("@ModuleId", moduleId)),
                "Module linked. Existing students in that course and year were registered for it.");
            return RedirectToAction("Courses");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UnlinkModule(int courseId, int moduleId)
        {
            TryDb(() => Db.Exec("sp_CourseModule_Unlink", Db.P("@CourseId", courseId), Db.P("@ModuleId", moduleId)), "Module unlinked from the course.");
            return RedirectToAction("Courses");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteCourse(int id)
        {
            TryDb(() => Db.Exec("sp_Course_Delete", Db.P("@CourseId", id)), "Course deleted.");
            return RedirectToAction("Courses");
        }

        // ---------- Oversight ----------
        public ActionResult Attendance()
        {
            return View(Db.List<AttendanceRecord>("sp_Attendance_Records", Db.P("@LecturerId", null)));
        }

        public ActionResult Analytics()
        {
            var ds = Db.QuerySet("sp_Admin_Analytics");
            try { ViewBag.Detail = Db.QuerySet("sp_Admin_AnalyticsDetail"); }
            catch (Exception ex) { AppLog.Error("Analytics detail", ex); ViewBag.Detail = null; }
            return View(new AnalyticsViewModel
            {
                RegistrationTrend = Db.Map<LabelValue>(ds.Tables[0]),
                RoleBreakdown = Db.Map<LabelValue>(ds.Tables[1]),
                ApplicationFunnel = Db.Map<LabelValue>(ds.Tables[2]),
                Students = Db.Map<StudentDirectoryEntry>(ds.Tables[3]),
                CourseYears = Db.Map<CourseYearGroup>(ds.Tables[4]),
                AttendanceByModule = Db.Map<ModuleSummary>(ds.Tables[5]),
                ModulesWithoutTutor = Db.Map<Module>(ds.Tables[6]),
                Coverage = Db.Map<TutorAssignment>(ds.Tables[7]),
                TutorActivity = Db.Map<TutorActivity>(ds.Tables[8])
            });
        }

        public ActionResult Transactions()
        {
            var ds = Db.QuerySet("sp_Admin_Transactions");
            ViewBag.Provider = PayoutProviderFactory.Create().Name;
            return View(new TransactionsViewModel { Payments = Db.Map<Payment>(ds.Tables[0]), Summary = Db.Row(ds.Tables[1]) });
        }

        /// <summary>Only needed if a provider call failed; normal payouts need no admin action.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RetryPayout(int paymentId)
        {
            string status = null;
            if (TryDb(() => status = PayoutService.Process(paymentId)))
            {
                if (status == "Sent") Flash("Payout sent.");
                else if (status == "NoBankDetails") Flash("The tutor still has no banking details. It will send automatically once they add them.", "info");
                else FlashError("The payout provider reported a failure. Check the message on the row.");
            }
            return RedirectToAction("Transactions");
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
