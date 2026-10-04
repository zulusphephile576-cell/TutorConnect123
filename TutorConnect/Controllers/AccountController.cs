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
    public class AccountController : BaseController
    {
        private static readonly string[] Roles = { "Student", "Tutor", "Lecturer", "Admin" };

        // ---------- Login ----------
        [HttpGet]
        public ActionResult Login(string returnUrl, string role, string email, bool registered = false)
        {
            if (IsLoggedIn) return DashboardFor(CurrentRole);
            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl,
                Role = Roles.Contains(role) ? role : "Student",
                Email = email,
                JustRegistered = registered
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!Roles.Contains(model.Role)) ModelState.AddModelError("Role", "Choose how you're signing in.");
            if (!ModelState.IsValid) return View(model);

            User user = null;
            if (!TryDbForm(() => user = Db.Single<User>("sp_User_GetByEmailRole", Db.P("@Email", model.Email.Trim()), Db.P("@Role", model.Role))))
                return View(model);

            if (user != null && user.IsLockedOut)
            {
                ModelState.AddModelError("", "Too many failed attempts. For your security this account is locked until "
                    + user.LockoutUntil.Value.ToString("HH:mm") + " (SAST). You can also reset your password.");
                return View(model);
            }
            if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                var message = "That email and password don't match a " + model.Role.ToLowerInvariant() + " account. Check the tab you're signing in on.";
                if (user != null)
                {
                    try
                    {
                        var row = Db.Row(Db.Query("sp_User_LoginFailed", Db.P("@UserId", user.UserId)));
                        var failed = row.ContainsKey("FailedLoginCount") && row["FailedLoginCount"] != null ? Convert.ToInt32(row["FailedLoginCount"]) : 0;
                        if (failed >= 5) message = "Too many failed attempts. For your security this account is locked for 15 minutes. You can also reset your password.";
                        else if (failed >= 3) message += " " + (5 - failed) + (5 - failed == 1 ? " attempt" : " attempts") + " left before the account is locked for 15 minutes.";
                    }
                    catch (Exception ex) { AppLog.Error("LoginFailed", ex); }
                }
                ModelState.AddModelError("", message);
                return View(model);
            }
            if (!user.IsActive)
            {
                ModelState.AddModelError("", "This account has been deactivated. Contact an administrator if you think this is a mistake.");
                return View(model);
            }

            try { Db.Exec("sp_User_LoginSucceeded", Db.P("@UserId", user.UserId)); }
            catch (Exception ex) { AppLog.Error("LoginSucceeded", ex); }

            SignIn(user);
            if (!user.PopiaConsentAt.HasValue) Session["NeedsConsent"] = true;
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
            return DashboardFor(user.Role);
        }

        private void SignIn(User user)
        {
            Session.Clear();
            Session["UserId"] = user.UserId;
            Session["Role"] = user.Role;
            Session["Name"] = user.FullName;
            Session["FirstName"] = user.FirstName;
            Session["Email"] = user.Email;
            Session["DarkMode"] = user.DarkMode;
            Session["YearOfStudy"] = user.YearOfStudy;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Flash("You've been signed out.");
            return RedirectToAction("Login");
        }

        // ---------- Registration ----------
        [HttpGet]
        public ActionResult Register()
        {
            if (IsLoggedIn) return DashboardFor(CurrentRole);
            var model = new RegisterViewModel();
            FillCatalog(model);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            var catalog = FillCatalog(model);
            foreach (var e in catalog.Validate(model.FacultyId, model.DepartmentId, model.CourseId))
                if (!ModelState.ContainsKey(e.Key) || ModelState[e.Key].Errors.Count == 0) ModelState.AddModelError(e.Key, e.Value);

            foreach (var e in RegistrationRules.Validate(model, TutorConnect.Helpers.Clock.Today))
                ModelState.AddModelError(e.Key, e.Value);
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim();
            var ok = TryDbForm(() => Db.ScalarInt("sp_Student_Register",
                Db.P("@FirstName", model.FirstName.Trim()), Db.P("@LastName", model.LastName.Trim()),
                Db.P("@Email", email), Db.P("@PasswordHash", PasswordHasher.Hash(model.Password)),
                Db.P("@StudentNumber", model.StudentNumber.Trim().ToUpperInvariant()), Db.P("@Phone", model.Phone.Trim()),
                Db.P("@CourseId", model.CourseId), Db.P("@YearOfStudy", model.YearOfStudy),
                Db.P("@DateOfBirth", model.DateOfBirth.Value.Date), Db.P("@Gender", Clean(model.Gender)), Db.P("@Campus", Clean(model.Campus)),
                Db.P("@EmergencyContactName", Clean(model.EmergencyContactName)), Db.P("@EmergencyContactPhone", Clean(model.EmergencyContactPhone)),
                Db.P("@PopiaConsent", model.AcceptPrivacy)));
            if (!ok) return View(model);

            // Account created: the student signs in themselves (no automatic sign-in).
            Flash("Your account is ready. Sign in with your university email and the password you just chose.");
            return RedirectToAction("Login", new { role = "Student", email, registered = true });
        }

        private static CourseCatalog FillCatalog(RegisterViewModel model)
        {
            var catalog = CourseCatalog.Load();
            model.Faculties = catalog.Faculties;
            model.Departments = catalog.Departments;
            model.Courses = catalog.Courses;
            return catalog;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // ---------- Forgot / reset password ----------
        [HttpGet]
        public ActionResult ForgotPassword(string role)
        {
            return View(new ForgotPasswordViewModel { Role = Roles.Contains(role) ? role : "Student" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!Roles.Contains(model.Role)) ModelState.AddModelError("Role", "Choose your account type.");
            if ((model.Role == "Student" || model.Role == "Tutor") && string.IsNullOrWhiteSpace(model.StudentNumber))
                ModelState.AddModelError("StudentNumber", "Enter your student number to confirm it's you.");
            if (!ModelState.IsValid) return View(model);

            var userId = Db.ScalarInt("sp_User_VerifyIdentity", Db.P("@Email", model.Email.Trim()), Db.P("@Role", model.Role),
                Db.P("@LastName", model.LastName.Trim()), Db.P("@StudentNumber", (model.StudentNumber ?? "").Trim()));

            if (userId == 0)
            {
                ModelState.AddModelError("", "We couldn't confirm those details. Check them against what you registered with.");
                return View(model);
            }

            var token = PasswordHasher.NewToken();
            Db.Exec("sp_PasswordReset_Create", Db.P("@UserId", userId), Db.P("@Token", token));
            return RedirectToAction("ResetPassword", new { token });
        }

        [HttpGet]
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token) || Db.ScalarInt("sp_PasswordReset_Check", Db.P("@Token", token)) == 0)
            {
                FlashError("That reset link has expired or was already used. Confirm your details again.");
                return RedirectToAction("ForgotPassword");
            }
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var ok = Db.ScalarInt("sp_PasswordReset_Consume", Db.P("@Token", model.Token), Db.P("@PasswordHash", PasswordHasher.Hash(model.NewPassword)));
            if (ok == 0)
            {
                FlashError("That reset link has expired. Confirm your details again.");
                return RedirectToAction("ForgotPassword");
            }
            Flash("Your password was changed. Sign in with the new one.");
            return RedirectToAction("Login");
        }

        // ---------- Settings (all roles) ----------
        [HttpGet, RoleAuthorize]
        public ActionResult Settings()
        {
            var user = Db.Single<User>("sp_User_GetById", Db.P("@UserId", CurrentUserId));
            ViewBag.PasswordModel = new ChangePasswordViewModel();
            ViewBag.Tokens = LoadTokens();
            return View(new SettingsViewModel { User = user, DarkMode = user.DarkMode, EmailNotifications = user.EmailNotifications });
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult Settings(SettingsViewModel model)
        {
            if (TryDb(() => Db.Exec("sp_User_UpdateSettings", Db.P("@UserId", CurrentUserId),
                    Db.P("@DarkMode", model.DarkMode), Db.P("@EmailNotifications", model.EmailNotifications)), "Your preferences were saved."))
            {
                Session["DarkMode"] = model.DarkMode;
            }
            return RedirectToAction("Settings");
        }

        /// <summary>Quick theme toggle from the top bar (AJAX).</summary>
        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult ToggleTheme()
        {
            var user = Db.Single<User>("sp_User_GetById", Db.P("@UserId", CurrentUserId));
            var dark = !user.DarkMode;
            Db.Exec("sp_User_UpdateSettings", Db.P("@UserId", CurrentUserId), Db.P("@DarkMode", dark), Db.P("@EmailNotifications", user.EmailNotifications));
            Session["DarkMode"] = dark;
            return Json(new { ok = true, dark });
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult Profile(ProfileViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Gender) && !RegisterViewModel.Genders.Contains(model.Gender))
                ModelState.AddModelError("Gender", "Choose an option from the list.");
            if (!ModelState.IsValid)
            {
                FlashError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return RedirectToAction("Settings");
            }
            TryDb(() => Db.Exec("sp_User_UpdateProfile", Db.P("@UserId", CurrentUserId), Db.P("@Phone", Clean(model.Phone)),
                    Db.P("@Campus", Clean(model.Campus)), Db.P("@Gender", Clean(model.Gender)),
                    Db.P("@EmergencyContactName", Clean(model.EmergencyContactName)), Db.P("@EmergencyContactPhone", Clean(model.EmergencyContactPhone))),
                "Your profile was saved.");
            return RedirectToAction("Settings");
        }

        private List<ApiTokenInfo> LoadTokens()
        {
            try { return Db.List<ApiTokenInfo>("sp_ApiToken_List", Db.P("@UserId", CurrentUserId)); }
            catch (Exception ex) { AppLog.Error("LoadTokens", ex); return new List<ApiTokenInfo>(); }
        }

        // ---------- POPIA ----------

        /// <summary>Right of access: downloads everything TutorConnect holds about the signed-in user as JSON.</summary>
        [HttpGet, RoleAuthorize]
        public ActionResult MyData()
        {
            var data = PrivacyService.Export(CurrentUserId);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(data);
            LogActivity(CurrentUserId, "You downloaded a copy of your personal information.");
            var name = "tutorconnect-my-data-" + TutorConnect.Helpers.Clock.Now.ToString("yyyy-MM-dd") + ".json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", name);
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult RequestDeletion(bool cancel = false)
        {
            if (CurrentRole == "Admin" && !cancel)
            {
                FlashError("Administrator accounts can't request deletion here. Ask another administrator.");
                return RedirectToAction("Settings");
            }
            TryDb(() => Db.Exec("sp_User_RequestDeletion", Db.P("@UserId", CurrentUserId), Db.P("@Cancel", cancel)),
                cancel ? "Your deletion request was cancelled." : "Deletion requested. An administrator will remove your account and let you know. Records needed for payments are kept as the law requires.");
            return RedirectToAction("Settings");
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult AcceptPrivacy(string returnUrl)
        {
            if (TryDb(() => Db.Exec("sp_User_RecordConsent", Db.P("@UserId", CurrentUserId)), "Thank you. Your consent has been recorded."))
                Session.Remove("NeedsConsent");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return DashboardFor(CurrentRole);
        }

        // ---------- REST API tokens ----------

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult CreateApiToken(string name, int days = ApiTokenService.DefaultDays)
        {
            DateTime expires;
            string token = null;
            if (TryDb(() => token = ApiTokenService.Create(CurrentUserId, name, days, out expires)))
            {
                TempData["NewApiToken"] = token;
                Flash("Token created. Copy it now: it won't be shown again.");
            }
            return Redirect(Url.Action("Settings") + "#api");
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult RevokeApiToken(int? id)
        {
            TryDb(() => Db.ScalarInt("sp_ApiToken_Revoke", Db.P("@UserId", CurrentUserId), Db.P("@TokenId", id), Db.P("@TokenHash", null)),
                id.HasValue ? "Token revoked. Apps using it can no longer connect." : "All your tokens were revoked.");
            return Redirect(Url.Action("Settings") + "#api");
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var user = Db.Single<User>("sp_User_GetById", Db.P("@UserId", CurrentUserId));
            if (ModelState.IsValid && !PasswordHasher.Verify(model.CurrentPassword, user.PasswordHash))
                ModelState.AddModelError("CurrentPassword", "Your current password is incorrect.");

            if (!ModelState.IsValid)
            {
                ViewBag.PasswordModel = model;
                ViewBag.OpenPassword = true;
                ViewBag.Tokens = LoadTokens();
                return View("Settings", new SettingsViewModel { User = user, DarkMode = user.DarkMode, EmailNotifications = user.EmailNotifications });
            }

            TryDb(() => Db.Exec("sp_User_ChangePassword", Db.P("@UserId", CurrentUserId), Db.P("@PasswordHash", PasswordHasher.Hash(model.NewPassword))),
                "Your password was changed.");
            return RedirectToAction("Settings");
        }
    }
}
