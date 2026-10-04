using System;
using System.Data;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Helpers;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers.Api
{
    /// <summary>
    /// TutorConnect REST API, version 1. Base URL: /api/v1. All times are South African Standard Time
    /// (ISO 8601 with +02:00) and all amounts are in South African Rand (ZAR).
    /// A ready-made Postman collection with tests is in the Testing folder of the solution.
    /// </summary>
    [RoutePrefix("api/v1")]
    public class ApiV1Controller : ApiControllerBase
    {
        private static readonly string[] Roles = { "Student", "Tutor", "Lecturer", "Admin" };

        // ------------------------------------------------------------------ public

        /// <summary>GET /api/v1: lists every endpoint, so the API describes itself.</summary>
        [HttpGet, Route("")]
        public ActionResult Index()
        {
            Func<string, string, string, string, object> e = (method, path, auth, what) => new { method, path = "/api/v1" + path, auth, description = what };
            return Ok(new
            {
                name = "TutorConnect REST API",
                version = "1.0",
                authentication = "Send 'Authorization: Bearer tc_...' (create a token with POST /api/v1/auth/token or under Settings, API access). GET requests also accept the signed-in website session.",
                timeZone = "Times are South African Standard Time, ISO 8601 with +02:00",
                currency = "Amounts are South African rand (ZAR)",
                endpoints = new[]
                {
                    e("GET", "/health", "none", "API and database status"),
                    e("POST", "/auth/token", "none", "Exchange email, password and role for a bearer token"),
                    e("DELETE", "/auth/token", "token", "Revoke the token used for this request"),
                    e("GET", "/courses", "none", "Courses offered"),
                    e("GET", "/faculties", "none", "Faculties"),
                    e("GET", "/faculties/{id}/departments", "none", "Departments in a faculty"),
                    e("GET", "/departments/{id}/courses", "none", "Courses in a department"),
                    e("GET", "/holidays?year=2026", "none", "South African public holidays (from the Nager.Date API)"),
                    e("GET", "/me", "any role", "Your profile"),
                    e("GET", "/me/data", "any role", "Everything held about you (POPIA right of access)"),
                    e("GET", "/modules", "any role", "Modules (students: your registered modules)"),
                    e("GET", "/notifications", "any role", "Your latest notifications"),
                    e("GET", "/tutors?q=", "Student", "Tutors for your modules and year"),
                    e("GET", "/tutors/{tutorId}/slots", "Student", "A tutor's open slots"),
                    e("GET", "/bookings", "Student, Tutor", "Your bookings or sessions"),
                    e("POST", "/bookings/{id}/cancel", "Student (token)", "Cancel an upcoming booking"),
                    e("PUT", "/bookings/{id}/status", "Tutor (token)", "Confirm, decline or cancel a booking"),
                    e("GET", "/tutorials", "Student, Tutor", "Your tutorials"),
                    e("GET", "/tutorials/{id}/register", "Tutor", "A tutorial's register"),
                    e("PUT", "/tutorials/{id}/attendance/{studentId}", "Tutor (token)", "Mark a student Present or Absent")
                }
            });
        }

        /// <summary>GET /api/v1/health: is the API up and is the database ready?</summary>
        [HttpGet, Route("health")]
        public ActionResult Health()
        {
            var db = DatabaseSetup.Check();
            return Ok(new
            {
                status = db.Ready ? "ok" : "degraded",
                database = db.Ready ? "ready" : (db.ServerReachable ? "needs setup" : "unreachable"),
                version = "1.0",
                serverTime = Clock.Iso(Clock.Now),
                timeZone = "Africa/Johannesburg (SAST, UTC+02:00)",
                currency = "ZAR"
            }, db.Ready ? 200 : 503);
        }

        /// <summary>POST /api/v1/auth/token: exchange email + password + role for a bearer token.</summary>
        [HttpPost, Route("auth/token")]
        public ActionResult CreateToken(string email, string password, string role, string name, int? days)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || !Roles.Contains(role))
                return Error(400, "invalid_request", "Send email, password and role (Student, Tutor, Lecturer or Admin).");

            var user = Db.Single<User>("sp_User_GetByEmailRole", Db.P("@Email", email.Trim()), Db.P("@Role", role));
            if (user != null && user.IsLockedOut)
                return Error(423, "locked", "Too many failed sign-ins. Try again after " + user.LockoutUntil.Value.ToString("HH:mm") + " SAST.");
            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                if (user != null) Db.Query("sp_User_LoginFailed", Db.P("@UserId", user.UserId));
                return Error(401, "invalid_credentials", "The email, password and role don't match an account.");
            }
            if (!user.IsActive) return Error(403, "inactive", "This account has been deactivated.");

            Db.Exec("sp_User_LoginSucceeded", Db.P("@UserId", user.UserId));
            DateTime expires;
            var token = ApiTokenService.Create(user.UserId, string.IsNullOrWhiteSpace(name) ? "API sign-in" : name, days ?? ApiTokenService.DefaultDays, out expires);
            return Ok(new
            {
                token,
                tokenType = "Bearer",
                expiresAt = Clock.Iso(expires),
                user = new { id = user.UserId, name = user.FullName, email = user.Email, role = user.Role }
            }, 201);
        }

        /// <summary>DELETE /api/v1/auth/token: revoke the token used for this request.</summary>
        [HttpDelete, Route("auth/token"), ApiAuthorize]
        public ActionResult RevokeToken()
        {
            var token = (Request.Headers["Authorization"] ?? "").Substring(7).Trim();
            Db.ScalarInt("sp_ApiToken_Revoke", Db.P("@UserId", Caller.UserId), Db.P("@TokenId", null), Db.P("@TokenHash", ApiTokenService.Hash(token)));
            return Ok(new { revoked = true });
        }

        /// <summary>GET /api/v1/courses: courses offered (public, used by registration).</summary>
        [HttpGet, Route("courses")]
        public ActionResult Courses()
        {
            return Ok(Db.List<Course>("sp_Course_List").Select(c => new { id = c.CourseId, code = c.Code, name = c.Name, description = c.Description }));
        }

        /// <summary>GET /api/v1/faculties: faculties with how many departments each has (public, used by registration).</summary>
        [HttpGet, Route("faculties")]
        public ActionResult Faculties()
        {
            var catalog = CourseCatalog.Load();
            return Ok(catalog.Faculties.Select(f => new
            {
                id = f.FacultyId, name = f.Name,
                departments = catalog.Departments.Count(d => d.FacultyId == f.FacultyId)
            }));
        }

        /// <summary>GET /api/v1/faculties/{id}/departments: departments in one faculty.</summary>
        [HttpGet, Route("faculties/{id:int}/departments")]
        public ActionResult FacultyDepartments(int id)
        {
            var catalog = CourseCatalog.Load();
            if (catalog.Faculties.All(f => f.FacultyId != id)) return Error(404, "not_found", "There's no faculty with that id.");
            return Ok(catalog.Departments.Where(d => d.FacultyId == id).Select(d => new
            {
                id = d.DepartmentId, facultyId = d.FacultyId, name = d.Name,
                courses = catalog.Courses.Count(c => c.DepartmentId == d.DepartmentId)
            }));
        }

        /// <summary>GET /api/v1/departments/{id}/courses: courses offered by one department.</summary>
        [HttpGet, Route("departments/{id:int}/courses")]
        public ActionResult DepartmentCourses(int id)
        {
            var catalog = CourseCatalog.Load();
            var dep = catalog.Departments.FirstOrDefault(d => d.DepartmentId == id);
            if (dep == null) return Error(404, "not_found", "There's no department with that id.");
            return Ok(catalog.Courses.Where(c => c.DepartmentId == id).Select(c => new
            {
                id = c.CourseId, code = c.Code, name = c.Name, departmentId = id, department = dep.Name, facultyId = dep.FacultyId
            }));
        }

        /// <summary>GET /api/v1/holidays?year=2026: South African public holidays (consumed from the Nager.Date API).</summary>
        [HttpGet, Route("holidays")]
        public ActionResult Holidays(int? year)
        {
            var y = year ?? Clock.Today.Year;
            if (y < 2000 || y > 2100) return Error(400, "invalid_year", "Choose a year between 2000 and 2100.");
            string source;
            var items = HolidayService.ForYear(y, out source);
            return Ok(new { year = y, country = "ZA", source, holidays = items.Select(h => new { date = h.Date.ToString("yyyy-MM-dd"), name = h.Name, weekday = h.Date.DayOfWeek.ToString() }) });
        }

        // ------------------------------------------------------------------ any signed-in user

        /// <summary>GET /api/v1/me: the signed-in user's profile.</summary>
        [HttpGet, Route("me"), ApiAuthorize]
        public ActionResult Me()
        {
            var u = Db.Single<User>("sp_User_GetById", Db.P("@UserId", Caller.UserId));
            if (u == null) return Error(404, "not_found", "Account not found.");
            return Ok(new
            {
                id = u.UserId, firstName = u.FirstName, lastName = u.LastName, email = u.Email, role = u.Role,
                studentNumber = u.StudentNumber, phone = u.Phone, course = u.CourseName, yearOfStudy = u.YearOfStudy,
                campus = u.Campus, createdAt = Clock.Iso(u.CreatedAt), privacyConsentAt = Clock.Iso(u.PopiaConsentAt)
            });
        }

        /// <summary>GET /api/v1/me/data: everything held about the caller (POPIA right of access).</summary>
        [HttpGet, Route("me/data"), ApiAuthorize]
        public ActionResult MyData()
        {
            return Ok(PrivacyService.Export(Caller.UserId));
        }

        /// <summary>GET /api/v1/modules: all modules with their lecturer and number of tutors.</summary>
        [HttpGet, Route("modules"), ApiAuthorize]
        public ActionResult Modules()
        {
            return Ok(Db.List<Module>("sp_Api_Modules").Select(m => new { id = m.ModuleId, code = m.Code, name = m.Name, yearLevel = m.YearLevel, lecturer = m.LecturerName, tutors = m.TutorCount }));
        }

        /// <summary>GET /api/v1/notifications: latest notifications and the unread count.</summary>
        [HttpGet, Route("notifications"), ApiAuthorize]
        public ActionResult Notifications()
        {
            var ds = Db.QuerySet("sp_Notifications_Get", Db.P("@UserId", Caller.UserId));
            var unread = ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 ? Convert.ToInt32(ds.Tables[1].Rows[0][0]) : 0;
            return Ok(new { unread, items = Db.Map<ActivityItem>(ds.Tables[0]).Select(a => new { message = a.Message, createdAt = Clock.Iso(a.CreatedAt), unread = a.IsUnread }) });
        }

        // ------------------------------------------------------------------ students

        /// <summary>GET /api/v1/tutors?q=ITPR: tutors covering the student's modules for their year.</summary>
        [HttpGet, Route("tutors"), ApiAuthorize("Student")]
        public ActionResult Tutors(string q)
        {
            var rows = Db.List<TutorAssignment>("sp_Student_SearchTutors", Db.P("@StudentId", Caller.UserId), Db.P("@Term", q));
            return Ok(rows.GroupBy(r => r.TutorId).Select(g => new
            {
                id = g.Key,
                name = g.First().TutorName,
                email = g.First().TutorEmail,
                openSlots = g.First().OpenSlots,
                modules = g.Select(m => new { id = m.ModuleId, code = m.ModuleCode, name = m.ModuleName, yearOfStudy = m.YearOfStudy })
            }));
        }

        /// <summary>GET /api/v1/tutors/{id}/slots: that tutor's open slots (only for the student's own tutors).</summary>
        [HttpGet, Route("tutors/{tutorId:int}/slots"), ApiAuthorize("Student")]
        public ActionResult TutorSlots(int tutorId)
        {
            var mine = Db.List<TutorAssignment>("sp_Student_SearchTutors", Db.P("@StudentId", Caller.UserId), Db.P("@Term", null));
            if (mine.All(a => a.TutorId != tutorId)) return Error(404, "not_found", "No tutor with that id covers your modules.");
            var rate = AppConfig.HourlyRate;
            return Ok(Db.List<Slot>("sp_Tutor_OpenSlots", Db.P("@TutorId", tutorId)).Select(s => new
            {
                id = s.SlotId, start = Clock.Iso(s.StartTime), end = Clock.Iso(s.EndTime), maxHours = Math.Min(8, s.MaxHours),
                hourlyRate = rate, currency = "ZAR", publicHoliday = Holiday(s.StartTime)
            }));
        }

        /// <summary>GET /api/v1/bookings: a student's bookings, or a tutor's sessions.</summary>
        [HttpGet, Route("bookings"), ApiAuthorize("Student", "Tutor")]
        public ActionResult Bookings(string status)
        {
            var rows = Caller.Role == "Student"
                ? Db.List<Booking>("sp_Student_Bookings", Db.P("@StudentId", Caller.UserId))
                : Db.List<Booking>("sp_Tutor_Sessions", Db.P("@TutorId", Caller.UserId));
            if (!string.IsNullOrEmpty(status)) rows = rows.Where(b => string.Equals(b.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
            return Ok(rows.Select(b => new
            {
                id = b.BookingId, module = b.ModuleCode, moduleName = b.ModuleName,
                tutor = b.TutorName, student = b.StudentName,
                start = Clock.Iso(b.StartTime), end = Clock.Iso(b.EndTime), hours = b.Hours,
                amount = b.Amount, amountDisplay = Ui.Money(b.Amount), currency = "ZAR",
                status = b.Status, attendance = b.AttendanceStatus
            }));
        }

        /// <summary>POST /api/v1/bookings/{id}/cancel: a student cancels an upcoming session.</summary>
        [HttpPost, Route("bookings/{id:int}/cancel"), ApiAuthorize("Student")]
        public ActionResult CancelBooking(int id)
        {
            var n = Db.ScalarInt("sp_Booking_CancelByStudent", Db.P("@BookingId", id), Db.P("@StudentId", Caller.UserId));
            return n == 1 ? Ok(new { id, status = "Cancelled" })
                          : Error(409, "not_cancellable", "Only your own upcoming pending or confirmed sessions can be cancelled.");
        }

        // ------------------------------------------------------------------ tutors

        /// <summary>PUT /api/v1/bookings/{id}/status  body: { "status": "Confirmed" | "Declined" | "Cancelled" }</summary>
        [HttpPut, Route("bookings/{id:int}/status"), ApiAuthorize("Tutor")]
        public ActionResult SetBookingStatus(int id, string status)
        {
            if (status != "Confirmed" && status != "Declined" && status != "Cancelled")
                return Error(400, "invalid_status", "Status must be Confirmed, Declined or Cancelled.");
            var n = Db.ScalarInt("sp_Booking_SetStatus", Db.P("@BookingId", id), Db.P("@TutorId", Caller.UserId), Db.P("@Status", status));
            return n == 1 ? Ok(new { id, status })
                          : Error(409, "invalid_transition", "That session isn't yours or can't move to " + status + " from its current status.");
        }

        /// <summary>GET /api/v1/tutorials: a student's tutorials, or the tutorials a tutor runs.</summary>
        [HttpGet, Route("tutorials"), ApiAuthorize("Student", "Tutor")]
        public ActionResult Tutorials()
        {
            var rows = Caller.Role == "Student"
                ? Db.List<Tutorial>("sp_Student_Tutorials", Db.P("@StudentId", Caller.UserId))
                : Db.List<Tutorial>("sp_Tutor_Tutorials", Db.P("@TutorId", Caller.UserId));
            return Ok(rows.Select(t => new
            {
                id = t.TutorialId, title = t.Title, module = t.ModuleCode, yearOfStudy = t.YearOfStudy, venue = t.Venue,
                start = Clock.Iso(t.StartTime), end = Clock.Iso(t.EndTime), tutor = t.TutorName,
                myAttendance = t.MyStatus, enrolled = t.EnrolledCount, present = t.PresentCount, late = t.LateCount, absent = t.AbsentCount
            }));
        }

        /// <summary>GET /api/v1/tutorials/{id}/register: the class list with attendance (tutor's own tutorials).</summary>
        [HttpGet, Route("tutorials/{id:int}/register"), ApiAuthorize("Tutor")]
        public ActionResult TutorialRegister(int id)
        {
            var t = Db.Single<Tutorial>("sp_Tutorial_Get", Db.P("@TutorialId", id), Db.P("@StudentId", null));
            if (t == null || t.TutorId != Caller.UserId) return Error(404, "not_found", "No tutorial of yours has that id.");
            return Ok(new
            {
                tutorial = new { id = t.TutorialId, title = t.Title, module = t.ModuleCode, start = Clock.Iso(t.StartTime), end = Clock.Iso(t.EndTime) },
                students = Db.List<RegisterEntry>("sp_Tutorial_Register", Db.P("@TutorialId", id)).Select(s => new
                {
                    id = s.StudentId, name = s.StudentName, studentNumber = s.StudentNumber, status = s.Status, method = s.Method, markedAt = Clock.Iso(s.MarkedAt)
                })
            });
        }

        /// <summary>PUT /api/v1/tutorials/{id}/attendance/{studentId}  body: { "status": "Present" | "Absent" }</summary>
        [HttpPut, Route("tutorials/{id:int}/attendance/{studentId:int}"), ApiAuthorize("Tutor")]
        public ActionResult MarkAttendance(int id, int studentId, string status)
        {
            if (status != "Present" && status != "Absent")
                return Error(400, "invalid_status", "Status must be Present or Absent.");
            Db.Exec("sp_Tutorial_MarkAttendance", Db.P("@TutorialId", id), Db.P("@StudentId", studentId), Db.P("@Status", status), Db.P("@TutorId", Caller.UserId));
            return Ok(new { tutorialId = id, studentId, status });
        }

        private static string Holiday(DateTime when)
        {
            var h = HolidayService.On(when.Date);
            return h == null ? null : h.Name;
        }

        /// <summary>Unknown /api/v1/... paths get a JSON 404 rather than an HTML page.</summary>
        [Route("{*path}", Order = 100)]
        public ActionResult NotFoundJson(string path)
        {
            return Error(404, "not_found", "There's no API endpoint at /api/v1/" + path + ".");
        }
    }
}
