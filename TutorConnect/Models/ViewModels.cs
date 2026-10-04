using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace TutorConnect.Models
{
    public class LoginViewModel
    {
        [Required, EmailAddress, Display(Name = "Email")] public string Email { get; set; }
        [Required, DataType(DataType.Password)] public string Password { get; set; }
        [Required] public string Role { get; set; } = "Student";
        public string ReturnUrl { get; set; }
        public bool JustRegistered { get; set; }
    }

    public class RegisterViewModel
    {
        public static readonly string[] Genders = { "Female", "Male", "Non-binary", "Prefer not to say" };

        // Personal
        [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; }
        [Required, StringLength(60), Display(Name = "Last name")] public string LastName { get; set; }
        [Required(ErrorMessage = "Enter your date of birth."), DataType(DataType.Date), Display(Name = "Date of birth")] public DateTime? DateOfBirth { get; set; }
        [StringLength(30), Display(Name = "Gender")] public string Gender { get; set; }
        [Required(ErrorMessage = "Enter a phone number we can reach you on."), Phone, StringLength(20), Display(Name = "Mobile number")] public string Phone { get; set; }

        // Studies
        [Required, EmailAddress, StringLength(150), Display(Name = "University email")] public string Email { get; set; }
        [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9]{5,20}$", ErrorMessage = "Use letters and digits only, 5 to 20 characters."), Display(Name = "Student number")] public string StudentNumber { get; set; }
        [Required(ErrorMessage = "Choose your course."), Display(Name = "Course")] public int? CourseId { get; set; }
        [Required, Range(1, 7), Display(Name = "Year of study")] public int? YearOfStudy { get; set; }
        [StringLength(80), Display(Name = "Campus")] public string Campus { get; set; }

        // Emergency contact
        [StringLength(120), Display(Name = "Contact name")] public string EmergencyContactName { get; set; }
        [Phone, StringLength(20), Display(Name = "Contact number")] public string EmergencyContactPhone { get; set; }

        // Account
        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)] public string Password { get; set; }
        [Required, Compare("Password", ErrorMessage = "The passwords don't match."), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; }
        [Display(Name = "I confirm these details are correct")] public bool AcceptTerms { get; set; }
        [Display(Name = "I accept the privacy notice")] public bool AcceptPrivacy { get; set; }

        [Required(ErrorMessage = "Choose your faculty."), Display(Name = "Faculty")] public int? FacultyId { get; set; }
        [Required(ErrorMessage = "Choose your department."), Display(Name = "Department")] public int? DepartmentId { get; set; }
        public List<Faculty> Faculties { get; set; } = new List<Faculty>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Course> Courses { get; set; } = new List<Course>();
    }

    public class ProfileViewModel
    {
        [Phone, StringLength(20), Display(Name = "Mobile number")] public string Phone { get; set; }
        [StringLength(80)] public string Campus { get; set; }
        [StringLength(30)] public string Gender { get; set; }
        [StringLength(120), Display(Name = "Emergency contact name")] public string EmergencyContactName { get; set; }
        [Phone, StringLength(20), Display(Name = "Emergency contact number")] public string EmergencyContactPhone { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress] public string Email { get; set; }
        [Required] public string Role { get; set; } = "Student";
        [Required, Display(Name = "Last name")] public string LastName { get; set; }
        [Display(Name = "Student number")] public string StudentNumber { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required] public string Token { get; set; }
        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; }
        [Required, Compare("NewPassword", ErrorMessage = "The passwords don't match."), DataType(DataType.Password), Display(Name = "Confirm new password")] public string ConfirmPassword { get; set; }
    }

    public class SettingsViewModel
    {
        public User User { get; set; }
        public bool DarkMode { get; set; }
        public bool EmailNotifications { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; }
        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; }
        [Required, Compare("NewPassword", ErrorMessage = "The passwords don't match."), DataType(DataType.Password), Display(Name = "Confirm new password")] public string ConfirmPassword { get; set; }
    }

    /// <summary>Shared container for the four role dashboards.</summary>
    public class DashboardViewModel
    {
        public Dictionary<string, object> Stats { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public List<Booking> Sessions { get; set; } = new List<Booking>();
        public List<Booking> TodaySessions { get; set; } = new List<Booking>();
        public List<Booking> PendingRequests { get; set; } = new List<Booking>();
        public List<Tutorial> Tutorials { get; set; } = new List<Tutorial>();
        public List<ActivityItem> Activity { get; set; } = new List<ActivityItem>();
        public List<Announcement> Announcements { get; set; } = new List<Announcement>();
        public List<TutorAssignment> Tutors { get; set; } = new List<TutorAssignment>();
        public List<Slot> NewSlots { get; set; } = new List<Slot>();
        public List<ContentItem> NewContent { get; set; } = new List<ContentItem>();
        public List<TutorApplication> Applications { get; set; } = new List<TutorApplication>();

        public int Stat(string key)
        {
            object v;
            return Stats.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v) : 0;
        }

        public decimal StatDecimal(string key)
        {
            object v;
            return Stats.TryGetValue(key, out v) && v != null ? Convert.ToDecimal(v) : 0m;
        }
    }

    public class BookViewModel
    {
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public List<TutorAssignment> Modules { get; set; } = new List<TutorAssignment>();
        public List<Slot> Slots { get; set; } = new List<Slot>();
        public decimal HourlyRate { get; set; }
        [Required(ErrorMessage = "Choose a module.")] public int? ModuleId { get; set; }
        [Required(ErrorMessage = "Choose a time slot.")] public int? SlotId { get; set; }
        [Range(1, 8)] public int Hours { get; set; } = 1;
        /// <summary>False when the tutor hasn't added a bank account, so payments to them can't go through yet.</summary>
        public bool TutorCanBePaid { get; set; } = true;
    }

    public class CheckoutViewModel
    {
        public Payment Payment { get; set; }
        public string ProcessUrl { get; set; }
        public List<KeyValuePair<string, string>> Fields { get; set; }
        public bool SimulationEnabled { get; set; }
    }

    public class TutorialFormViewModel
    {
        [Required(ErrorMessage = "Choose a module and year.")] public string Assignment { get; set; } // "moduleId|year"
        [Required, StringLength(150)] public string Title { get; set; }
        [StringLength(1000)] public string Description { get; set; }
        [StringLength(150)] public string Venue { get; set; }
        [Required, Display(Name = "Date")] public DateTime? Date { get; set; }
        [Required, Display(Name = "Start time")] public TimeSpan? StartTime { get; set; }
        [Required, Display(Name = "End time")] public TimeSpan? EndTime { get; set; }
        public List<TutorAssignment> Assignments { get; set; } = new List<TutorAssignment>();
    }

    public class RegisterSheetViewModel
    {
        public Tutorial Tutorial { get; set; }
        public ClassRegister ClassRegister { get; set; }
        public List<RegisterEntry> Students { get; set; } = new List<RegisterEntry>();
    }

    public class BankingViewModel
    {
        public BankingDetails Existing { get; set; }
        [Required, Display(Name = "Bank")] public string BankName { get; set; }
        [Required, StringLength(120), Display(Name = "Account holder")] public string AccountHolder { get; set; }
        [Required, Display(Name = "Account type")] public string AccountType { get; set; }
        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Branch codes are six digits."), Display(Name = "Branch code")] public string BranchCode { get; set; }
        [Required(ErrorMessage = "Enter the account number."), Display(Name = "Account number")] public string AccountNumber { get; set; }
    }

    public class ApplicationFormViewModel
    {
        [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; }
        [Required, StringLength(60), Display(Name = "Last name")] public string LastName { get; set; }
        [Required, StringLength(20), Display(Name = "Student number")] public string StudentNumber { get; set; }
        [Required, EmailAddress, Display(Name = "University email")] public string Email { get; set; }
        [Required, EmailAddress, Display(Name = "Personal email")] public string PersonalEmail { get; set; }
        [Phone] public string Phone { get; set; }
        [Required(ErrorMessage = "Choose your course."), Display(Name = "Course")] public int? CourseId { get; set; }
        [Required, Range(1, 7), Display(Name = "Your current year")] public int? CurrentYear { get; set; }
        [Required(ErrorMessage = "Choose the module you'd tutor."), Display(Name = "Module to tutor")] public int? ModuleId { get; set; }
        [Required, Range(1, 7), Display(Name = "Year of study you'd tutor")] public int? TutorYear { get; set; }
        [Required, Range(0, 100), Display(Name = "Your mark for this module (%)")] public decimal? ModuleMark { get; set; }
        [Required, StringLength(2000, MinimumLength = 50, ErrorMessage = "Write at least 50 characters about why you'd be a good tutor.")] public string Motivation { get; set; }
        public HttpPostedFileBase IdDocument { get; set; }
        public HttpPostedFileBase Transcript { get; set; }
        /// <summary>Curriculum vitae (PDF or Word). Required.</summary>
        public HttpPostedFileBase Cv { get; set; }
        [Required(ErrorMessage = "Choose your faculty."), Display(Name = "Faculty")] public int? FacultyId { get; set; }
        [Required(ErrorMessage = "Choose your department."), Display(Name = "Department")] public int? DepartmentId { get; set; }
        public List<Faculty> Faculties { get; set; } = new List<Faculty>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Course> Courses { get; set; } = new List<Course>();
        public List<Module> Modules { get; set; } = new List<Module>();
    }

    public class CreateLecturerViewModel
    {
        [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; }
        [Required, StringLength(60), Display(Name = "Last name")] public string LastName { get; set; }
        [Required, EmailAddress, Display(Name = "University email (login)")] public string Email { get; set; }
        [Required, EmailAddress, Display(Name = "Personal email (receives login details)")] public string PersonalEmail { get; set; }
        [Phone] public string Phone { get; set; }
    }

    public class CourseFormViewModel
    {
        [Required(ErrorMessage = "Choose the faculty this course falls under.")] public int? FacultyId { get; set; }
        [Required(ErrorMessage = "Choose the department this course falls under.")] public int? DepartmentId { get; set; }
        public List<Faculty> Faculties { get; set; } = new List<Faculty>();
        public List<Department> Departments { get; set; } = new List<Department>();
        [Required, StringLength(20)] public string Code { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(500)] public string Description { get; set; }
        public List<ModuleRow> Modules { get; set; } = new List<ModuleRow>();
    }

    public class ModuleRow
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int YearLevel { get; set; } = 1;
        public int Semester { get; set; } = 1;
        public int? LecturerId { get; set; }

        public bool IsBlank => string.IsNullOrWhiteSpace(Code) && string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// The card checkout. Only the cardholder, card brand, last four digits, expiry and billing address reach the
    /// server; the full card number and CVV stay in the browser and are never sent or stored.
    /// </summary>
    public class CardCheckoutViewModel
    {
        public Payment Payment { get; set; }
        public BankingDetails TutorBanking { get; set; }
        public string CardholderName { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; } = "South Africa";
        public string Error { get; set; }

        public static readonly string[] Countries = { "South Africa", "Botswana", "Eswatini", "Lesotho", "Mozambique", "Namibia", "Zambia", "Zimbabwe", "Other" };
        public static readonly string[] Brands = { "Visa", "Mastercard", "American Express" };
    }

    /// <summary>The bank-payment checkout: the student's bank and account, and where the money goes.</summary>
    public class BankCheckoutViewModel
    {
        public Payment Payment { get; set; }
        public BankingDetails TutorBanking { get; set; }
        public List<TutorConnect.Services.SouthAfricanBanks.Bank> Banks { get; set; }
        public string BankCode { get; set; }
        public string AccountHolder { get; set; }
        public string AccountType { get; set; }
        public string AccountNumber { get; set; }
        public string Error { get; set; }
    }

    public class AnalyticsViewModel
    {
        public List<LabelValue> RegistrationTrend { get; set; }
        public List<LabelValue> RoleBreakdown { get; set; }
        public List<LabelValue> ApplicationFunnel { get; set; }
        public List<StudentDirectoryEntry> Students { get; set; }
        public List<CourseYearGroup> CourseYears { get; set; }
        public List<ModuleSummary> AttendanceByModule { get; set; }
        public List<Module> ModulesWithoutTutor { get; set; }
        public List<TutorAssignment> Coverage { get; set; }
        public List<TutorActivity> TutorActivity { get; set; }
    }

    public class TransactionsViewModel
    {
        public List<Payment> Payments { get; set; }
        public Dictionary<string, object> Summary { get; set; }
    }

    public class ScheduleViewModel
    {
        public DateTime WeekStart { get; set; }
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
        public List<PublicHoliday> Holidays { get; set; } = new List<PublicHoliday>();
        public int FirstHour { get; set; } = 7;
        public int LastHour { get; set; } = 21;
        public string Role { get; set; }

        public static DateTime MondayOf(DateTime d)
        {
            var diff = ((int)d.DayOfWeek + 6) % 7;
            return d.Date.AddDays(-diff);
        }
    }

    /// <summary>Drives the shared faculty -> department -> course dropdowns (Views/Shared/_CourseCascade).</summary>
    public class CourseCascadeModel
    {
        public List<Faculty> Faculties { get; set; } = new List<Faculty>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Course> Courses { get; set; } = new List<Course>();
        public int? FacultyId { get; set; }
        public int? DepartmentId { get; set; }
        public int? CourseId { get; set; }
        public bool IncludeCourse { get; set; } = true;
        public string FacultyLabel { get; set; } = "Faculty";
        public string CourseHint { get; set; }
    }
}
