using System;
using System.Collections.Generic;
using System.Linq;

namespace TutorConnect.Models
{
    public class User
    {
        /// <summary>Comma lists used by the admin filters (faculty, department, course).</summary>
        public string FacultyIds { get; set; }
        public string DepartmentIds { get; set; }
        public string CourseIds { get; set; }
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
        public string StudentNumber { get; set; }
        public string Phone { get; set; }
        public int? CourseId { get; set; }
        public string CourseName { get; set; }
        public string CourseCode { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? FacultyId { get; set; }
        public string FacultyName { get; set; }
        public int? YearOfStudy { get; set; }
        public string PersonalEmail { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string Campus { get; set; }
        public string EmergencyContactName { get; set; }
        public string EmergencyContactPhone { get; set; }
        public DateTime? PopiaConsentAt { get; set; }
        public DateTime? DeletionRequestedAt { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public bool IsLockedOut => LockoutUntil.HasValue && LockoutUntil.Value > TutorConnect.Helpers.Clock.Now;
        public bool DarkMode { get; set; }
        public bool EmailNotifications { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FullName => (FirstName + " " + LastName).Trim();
    }

    public class Faculty
    {
        public int FacultyId { get; set; }
        public string Name { get; set; }
        public int DepartmentCount { get; set; }
        public int CourseCount { get; set; }
    }

    public class Department
    {
        public int DepartmentId { get; set; }
        public int FacultyId { get; set; }
        public string FacultyName { get; set; }
        public string Name { get; set; }
        public int CourseCount { get; set; }
    }

    public class Course
    {
        public int CourseId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? FacultyId { get; set; }
        public string FacultyName { get; set; }
        public int ModuleCount { get; set; }
        public int StudentCount { get; set; }
    }

    public class Module
    {
        /// <summary>1 = first semester, 2 = second semester.</summary>
        public int Semester { get; set; } = 1;
        /// <summary>Comma lists used by the admin filters (faculty, department, course).</summary>
        public string FacultyIds { get; set; }
        public string DepartmentIds { get; set; }
        public string CourseIds { get; set; }
        public int ModuleId { get; set; }
        public int CourseId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int YearLevel { get; set; }
        public string Description { get; set; }
        public int? LecturerId { get; set; }
        public string LecturerName { get; set; }
        public string Courses { get; set; }
        public int TutorCount { get; set; }
        public int StudentCount { get; set; }
        public int? TutorId { get; set; }
        public string TutorName { get; set; }
        /// <summary>How many tutors the lecturer wants for this module.</summary>
        public int TutorsNeeded { get; set; }
        /// <summary>Upcoming, unbooked availability slots offered by this module's tutors.</summary>
        public int OpenSlots { get; set; }
        public int OpenPositions => Math.Max(0, TutorsNeeded - TutorCount);
    }

    public class TutorAssignment
    {
        public int TutorModuleId { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public string TutorEmail { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int YearOfStudy { get; set; }
        public int OpenSlots { get; set; }
        public int StudentCount { get; set; }
        public DateTime AssignedAt { get; set; }
        public string AssignedByName { get; set; }
        public string Label => ModuleCode + " (year " + YearOfStudy + ")";
    }

    public class Slot
    {
        public int SlotId { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StudentName { get; set; }
        public int MaxHours => Math.Max(1, (int)Math.Floor((EndTime - StartTime).TotalHours));
    }

    public class Booking
    {
        public int BookingId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public string StudentNumber { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public string TutorEmail { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Hours { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string AttendanceStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsUpcoming => StartTime > TutorConnect.Helpers.Clock.Now;
    }

    public class Payment
    {
        public string PaymentMethod { get; set; }
        public string PayerBank { get; set; }
        public string PayerAccountLast4 { get; set; }
        public int PaymentId { get; set; }
        public string PaymentRef { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentFirstName { get; set; }
        public string StudentLastName { get; set; }
        public string StudentEmail { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public string TutorEmail { get; set; }
        public bool TutorWantsEmail { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int? SlotId { get; set; }
        public DateTime SessionStart { get; set; }
        public int Hours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string PfPaymentId { get; set; }
        public int? BookingId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? PayoutId { get; set; }
        public string PayoutStatus { get; set; }
        public string PayoutReference { get; set; }
        public string PayoutMessage { get; set; }
        public DateTime? PayoutProcessedAt { get; set; }
        public string AccountLast4 { get; set; }
    }

    public class Tutorial
    {
        public int TutorialId { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int YearOfStudy { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Venue { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Guid QrToken { get; set; }
        public string CheckInCode { get; set; }
        public int EnrolledCount { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public string MyStatus { get; set; }
        public bool IsEnrolled { get; set; }
        public bool IsPast => EndTime < TutorConnect.Helpers.Clock.Now;
        /// <summary>Check-in is open from the start of the tutorial until it ends.</summary>
        public bool IsLive => StartTime <= TutorConnect.Helpers.Clock.Now && EndTime >= TutorConnect.Helpers.Clock.Now;
        public int MarkedCount => PresentCount + LateCount + AbsentCount;
    }

    public class RegisterEntry
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public string Method { get; set; }
        public DateTime? MarkedAt { get; set; }
    }

    public class ClassRegister
    {
        public int RegisterId { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int YearOfStudy { get; set; }
        public string Title { get; set; }
        public DateTime RegisterDate { get; set; }
        public int EnrolledCount { get; set; }
        public int PresentCount { get; set; }
    }

    public class AttendanceRecord
    {
        public string Source { get; set; }
        public int SourceId { get; set; }
        public string Title { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string TutorName { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
    }

    public class ContentItem
    {
        public int ContentId { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int? LecturerId { get; set; }
        public int? YearOfStudy { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int? UploadedBy { get; set; }
        public string UploaderName { get; set; }
        public string UploaderRole { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool IsReviewed { get; set; }
        public string ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class Announcement
    {
        public int AnnouncementId { get; set; }
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int YearOfStudy { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TutorApplication
    {
        public int ApplicationId { get; set; }
        public int? StudentId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string StudentNumber { get; set; }
        public string Email { get; set; }
        public string PersonalEmail { get; set; }
        public string Phone { get; set; }
        public int? CourseId { get; set; }
        public string CourseName { get; set; }
        public int CurrentYear { get; set; }
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int TutorYear { get; set; }
        public decimal ModuleMark { get; set; }
        public string Motivation { get; set; }
        public string IdDocumentPath { get; set; }
        public string TranscriptPath { get; set; }
        public string CvPath { get; set; }
        public string Status { get; set; }
        public int? AssignedLecturerId { get; set; }
        public string LecturerName { get; set; }
        public string LecturerRecommendation { get; set; }
        public string LecturerComment { get; set; }
        public DateTime? RecommendedAt { get; set; }
        public string AdminFeedback { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string FullName => FirstName + " " + LastName;
        public bool IsDecided => Status == "Approved" || Status == "Rejected";
    }

    public class BankingDetails
    {
        public int TutorId { get; set; }
        public string BankName { get; set; }
        public string AccountHolder { get; set; }
        public string AccountType { get; set; }
        public string BranchCode { get; set; }
        public string AccountNumberProtected { get; set; }
        public string AccountLast4 { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ActivityItem
    {
        public int ActivityId { get; set; }
        /// <summary>Page the notification opens; null for older notifications (worked out from the message).</summary>
        public string Link { get; set; }
        public bool IsUnread { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
    }

    public class Reminder
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class LabelValue
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }

    public class StudentProgress
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public int? YearOfStudy { get; set; }
        public string ModuleCode { get; set; }
        public int Total { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public decimal Rate { get; set; }
    }

    public class TutorActivity
    {
        public int TutorId { get; set; }
        public string TutorName { get; set; }
        public int Sessions { get; set; }
        public int Hours { get; set; }
        public int Tutorials { get; set; }
        public int Registers { get; set; }
        public int Announcements { get; set; }
        public decimal PaidOut { get; set; }
    }

    public class ModuleSummary
    {
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public int Sessions { get; set; }
        public int Hours { get; set; }
        public int Total { get; set; }
        public int Present { get; set; }
        public decimal Rate { get; set; }
    }

    public class StudentDirectoryEntry
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string StudentNumber { get; set; }
        public int? YearOfStudy { get; set; }
        public string CourseName { get; set; }
        public int ModuleCount { get; set; }
        public int BookingCount { get; set; }
        public decimal AttendanceRate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CourseYearGroup
    {
        public string CourseName { get; set; }
        public int? YearOfStudy { get; set; }
        public int StudentCount { get; set; }
    }

    public class CheckInResult
    {
        public string Result { get; set; }
        public string Status { get; set; }
        public int? TutorialId { get; set; }
    }

    /// <summary>One block on the weekly schedule.</summary>
    public class CalendarEvent
    {
        public string Kind { get; set; }      // Session, Tutorial, Slot
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Code { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
    }

    public class ApiTokenInfo
    {
        public int TokenId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive => !RevokedAt.HasValue && ExpiresAt > TutorConnect.Helpers.Clock.Now;
    }

    /// <summary>The signed-in caller of a REST API request (from a bearer token or the web session).</summary>
    public class ApiUser
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int? YearOfStudy { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>A South African public holiday, read from the Nager.Date public holidays API.</summary>
    public class PublicHoliday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
    }

    /// <summary>A note on a tutor application, written by an administrator or the reviewing lecturer.</summary>
    public class ApplicationComment
    {
        public int CommentId { get; set; }
        public string Role { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string AuthorName { get; set; }
    }

    /// <summary>A tutor a lecturer can assign, with the modules they already tutor or were approved for.</summary>
    public class TutorPoolEntry
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string StudentNumber { get; set; }
        public string TutorsModules { get; set; }
        public string ApprovedModules { get; set; }
        public int OpenSlots { get; set; }
        public string FullName => FirstName + " " + LastName;
        public IEnumerable<string> AllModules =>
            ((TutorsModules ?? "") + "," + (ApprovedModules ?? "")).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct();
    }

    /// <summary>One module a tutor or lecturer is responsible for, with what's happening in it.</summary>
    public class ModuleOverview
    {
        public int ModuleId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int YearLevel { get; set; }
        public int Semester { get; set; } = 1;
        public string Description { get; set; }
        public int YearOfStudy { get; set; }
        public DateTime? AssignedAt { get; set; }
        public string LecturerName { get; set; }
        public string LecturerEmail { get; set; }
        public string Courses { get; set; }
        public string OtherTutors { get; set; }
        public int StudentCount { get; set; }
        public int TutorCount { get; set; }
        public int TutorsNeeded { get; set; }
        public int OpenSlots { get; set; }
        public int UpcomingTutorials { get; set; }
        public int UpcomingSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int TutorialCount { get; set; }
        public int ContentCount { get; set; }
        public int ContentToReview { get; set; }
        public decimal? AttendanceRate { get; set; }
    }

    /// <summary>A tutor on one of a lecturer's modules.</summary>
    public class ModuleTutor
    {
        public int ModuleId { get; set; }
        public int TutorId { get; set; }
        public int YearOfStudy { get; set; }
        public string TutorName { get; set; }
        public string TutorEmail { get; set; }
        public int OpenSlots { get; set; }
        public int CompletedSessions { get; set; }
    }
}
