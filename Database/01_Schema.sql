/* =====================================================================
   TutorConnect - 01_Schema.sql
   Requires SQL Server 2016 SP1 or later (uses CREATE OR ALTER / DROP IF EXISTS).
   Safe to re-run: drops every TutorConnect object and recreates it.
   ===================================================================== */
IF DB_ID('TutorConnect') IS NULL CREATE DATABASE TutorConnect;
GO
USE TutorConnect;
GO

/* ---------- Clean slate ---------- */
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.procedures;
EXEC sp_executesql @sql;
SET @sql = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' FROM sys.foreign_keys;
EXEC sp_executesql @sql;
SET @sql = N'';
SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.tables;
EXEC sp_executesql @sql;
DROP TYPE IF EXISTS dbo.ModuleListType;
GO

/* ---------- South African time ----------
   Every "now" in TutorConnect is South African Standard Time (UTC+2), whatever time zone the server uses. */
CREATE OR ALTER FUNCTION dbo.fn_SaNow() RETURNS DATETIME2 AS
BEGIN
    RETURN CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'South Africa Standard Time' AS DATETIME2);
END
GO

/* ---------- Academic structure ---------- */
/* ---------- Faculties and departments: every course belongs to one department ---------- */
CREATE TABLE dbo.Faculties (
    FacultyId    INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(120) NOT NULL CONSTRAINT UQ_Faculties_Name UNIQUE,
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TABLE dbo.Departments (
    DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
    FacultyId    INT           NOT NULL REFERENCES dbo.Faculties(FacultyId),
    Name         NVARCHAR(120) NOT NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT UQ_Departments_Faculty_Name UNIQUE (FacultyId, Name)
);

CREATE TABLE dbo.Courses (
    CourseId     INT IDENTITY(1,1) PRIMARY KEY,
    Code         NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Courses_Code UNIQUE,
    Name         NVARCHAR(150) NOT NULL,
    Description  NVARCHAR(500) NULL,
    DepartmentId INT           NULL REFERENCES dbo.Departments(DepartmentId),
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TABLE dbo.Users (
    UserId             INT IDENTITY(1,1) PRIMARY KEY,
    FirstName          NVARCHAR(60)  NOT NULL,
    LastName           NVARCHAR(60)  NOT NULL,
    Email              NVARCHAR(150) NOT NULL,
    PasswordHash       NVARCHAR(100) NOT NULL,
    Role               NVARCHAR(20)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Student','Tutor','Lecturer','Admin')),
    StudentNumber      NVARCHAR(20)  NULL,
    Phone              NVARCHAR(20)  NULL,
    CourseId           INT           NULL REFERENCES dbo.Courses(CourseId),
    YearOfStudy        INT           NULL CONSTRAINT CK_Users_Year CHECK (YearOfStudy BETWEEN 1 AND 7),
    PersonalEmail      NVARCHAR(150) NULL,
    DarkMode           BIT           NOT NULL DEFAULT 0,
    EmailNotifications BIT           NOT NULL DEFAULT 1,
    IsActive           BIT           NOT NULL DEFAULT 1,
    NotificationsSeenAt DATETIME2   NULL,
    DateOfBirth        DATE          NULL,
    Gender             NVARCHAR(30)  NULL,
    Campus             NVARCHAR(80)  NULL,
    EmergencyContactName  NVARCHAR(120) NULL,
    EmergencyContactPhone NVARCHAR(20)  NULL,
    PopiaConsentAt     DATETIME2     NULL,     -- when the user accepted the privacy notice (POPIA)
    DeletionRequestedAt DATETIME2    NULL,     -- user asked for their account to be deleted
    FailedLoginCount   INT           NOT NULL DEFAULT 0,
    LockoutUntil       DATETIME2     NULL,     -- sign-in blocked until this time after repeated failures
    CreatedAt          DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    -- A person can hold a Student and a Tutor account on the same email; the login tab picks the role.
    CONSTRAINT UQ_Users_Email_Role UNIQUE (Email, Role)
);

CREATE TABLE dbo.Modules (
    ModuleId     INT IDENTITY(1,1) PRIMARY KEY,
    Code         NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Modules_Code UNIQUE,
    Name         NVARCHAR(150) NOT NULL,
    YearLevel    INT           NOT NULL DEFAULT 1,
    Description  NVARCHAR(500) NULL,
    LecturerId   INT           NULL REFERENCES dbo.Users(UserId),
    TutorsNeeded INT           NOT NULL DEFAULT 1,      -- set by the lecturer: how many tutors this module needs
    Semester     INT           NOT NULL DEFAULT 1 CONSTRAINT CK_Modules_Semester CHECK (Semester IN (1, 2)),
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TABLE dbo.CourseModules (
    CourseId INT NOT NULL REFERENCES dbo.Courses(CourseId),
    ModuleId INT NOT NULL REFERENCES dbo.Modules(ModuleId),
    CONSTRAINT PK_CourseModules PRIMARY KEY (CourseId, ModuleId)
);

CREATE TABLE dbo.StudentModules (
    StudentId    INT       NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId     INT       NOT NULL REFERENCES dbo.Modules(ModuleId),
    RegisteredAt DATETIME2 NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT PK_StudentModules PRIMARY KEY (StudentId, ModuleId)
);

-- One tutor per module per year of study (reassigning upserts the row).
CREATE TABLE dbo.TutorModules (
    TutorModuleId INT IDENTITY(1,1) PRIMARY KEY,
    TutorId       INT       NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId      INT       NOT NULL REFERENCES dbo.Modules(ModuleId),
    YearOfStudy   INT       NOT NULL,
    AssignedBy    INT       NULL REFERENCES dbo.Users(UserId),
    AssignedAt    DATETIME2 NOT NULL DEFAULT dbo.fn_SaNow(),
    -- A module can have several tutors (up to Modules.TutorsNeeded); a tutor is assigned to it only once.
    CONSTRAINT UQ_TutorModules_Tutor UNIQUE (TutorId, ModuleId, YearOfStudy)
);

/* ---------- Booking & payments ---------- */
CREATE TABLE dbo.AvailabilitySlots (
    SlotId    INT IDENTITY(1,1) PRIMARY KEY,
    TutorId   INT       NOT NULL REFERENCES dbo.Users(UserId),
    StartTime DATETIME2 NOT NULL,
    EndTime   DATETIME2 NOT NULL,
    IsBooked  BIT       NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT CK_Slot_Time CHECK (EndTime > StartTime)
);

CREATE TABLE dbo.Payments (
    PaymentId    INT IDENTITY(1,1) PRIMARY KEY,
    PaymentRef   NVARCHAR(40)  NOT NULL CONSTRAINT UQ_Payments_Ref UNIQUE,
    StudentId    INT           NOT NULL REFERENCES dbo.Users(UserId),
    TutorId      INT           NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId     INT           NOT NULL REFERENCES dbo.Modules(ModuleId),
    SlotId       INT           NULL REFERENCES dbo.AvailabilitySlots(SlotId),
    SessionStart DATETIME2     NOT NULL,
    Hours        INT           NOT NULL,
    HourlyRate   DECIMAL(10,2) NOT NULL,
    Amount       DECIMAL(10,2) NOT NULL,
    Status       NVARCHAR(20)  NOT NULL DEFAULT 'Pending'
                 CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending','Complete','Failed','Cancelled')),
    PfPaymentId  NVARCHAR(50)  NULL,
    PaymentMethod NVARCHAR(20) NULL,               -- 'EFT' (bank payment) or 'PayFast'
    PayerBank    NVARCHAR(60)  NULL,               -- the student's bank; the full account number is never stored
    PayerAccountLast4 NVARCHAR(4) NULL,
    BookingId    INT           NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    CompletedAt  DATETIME2     NULL
);

CREATE TABLE dbo.Bookings (
    BookingId        INT IDENTITY(1,1) PRIMARY KEY,
    StudentId        INT           NOT NULL REFERENCES dbo.Users(UserId),
    TutorId          INT           NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId         INT           NOT NULL REFERENCES dbo.Modules(ModuleId),
    SlotId           INT           NULL REFERENCES dbo.AvailabilitySlots(SlotId),
    PaymentId        INT           NULL REFERENCES dbo.Payments(PaymentId),
    StartTime        DATETIME2     NOT NULL,
    EndTime          DATETIME2     NOT NULL,
    Hours            INT           NOT NULL,
    Amount           DECIMAL(10,2) NOT NULL,
    Status           NVARCHAR(20)  NOT NULL DEFAULT 'Pending'
                     CONSTRAINT CK_Bookings_Status CHECK (Status IN ('Pending','Confirmed','Declined','Cancelled','Completed')),
    AttendanceStatus NVARCHAR(10)  NULL CONSTRAINT CK_Bookings_Att CHECK (AttendanceStatus IN ('Present','Late','Absent')),
    Notes            NVARCHAR(500) NULL,
    CreatedAt        DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    UpdatedAt        DATETIME2     NULL
);

CREATE TABLE dbo.BankingDetails (
    TutorId                INT           PRIMARY KEY REFERENCES dbo.Users(UserId),
    BankName               NVARCHAR(80)  NOT NULL,
    AccountHolder          NVARCHAR(120) NOT NULL,
    AccountType            NVARCHAR(30)  NOT NULL,
    BranchCode             NVARCHAR(10)  NOT NULL,
    AccountNumberProtected NVARCHAR(512) NOT NULL,  -- encrypted with ASP.NET MachineKey, never plain text
    AccountLast4           NVARCHAR(4)   NOT NULL,
    UpdatedAt              DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TABLE dbo.Payouts (
    PayoutId          INT IDENTITY(1,1) PRIMARY KEY,
    PaymentId         INT           NOT NULL CONSTRAINT UQ_Payouts_Payment UNIQUE REFERENCES dbo.Payments(PaymentId),
    TutorId           INT           NOT NULL REFERENCES dbo.Users(UserId),
    Amount            DECIMAL(10,2) NOT NULL,
    Status            NVARCHAR(20)  NOT NULL DEFAULT 'Queued'
                      CONSTRAINT CK_Payouts_Status CHECK (Status IN ('Queued','Sent','Failed','NoBankDetails')),
    ProviderReference NVARCHAR(100) NULL,
    Message           NVARCHAR(500) NULL,
    CreatedAt         DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    ProcessedAt       DATETIME2     NULL
);

/* ---------- Tutorials & attendance ---------- */
CREATE TABLE dbo.Tutorials (
    TutorialId  INT IDENTITY(1,1) PRIMARY KEY,
    TutorId     INT              NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId    INT              NOT NULL REFERENCES dbo.Modules(ModuleId),
    YearOfStudy INT              NOT NULL,
    Title       NVARCHAR(150)    NOT NULL,
    Description NVARCHAR(1000)   NULL,
    Venue       NVARCHAR(150)    NULL,
    StartTime   DATETIME2        NOT NULL,
    EndTime     DATETIME2        NOT NULL,
    QrToken     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() CONSTRAINT UQ_Tutorials_Qr UNIQUE,
    CheckInCode NVARCHAR(8)      NULL,     -- short code students can type if they can't scan
    CreatedAt   DATETIME2        NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT CK_Tutorial_Time CHECK (EndTime > StartTime)
);

CREATE TABLE dbo.TutorialAttendance (
    TutorialId INT          NOT NULL REFERENCES dbo.Tutorials(TutorialId),
    StudentId  INT          NOT NULL REFERENCES dbo.Users(UserId),
    Status     NVARCHAR(10) NOT NULL CONSTRAINT CK_TA_Status CHECK (Status IN ('Present','Late','Absent')),
    Method     NVARCHAR(10) NOT NULL DEFAULT 'Manual' CONSTRAINT CK_TA_Method CHECK (Method IN ('Manual','QR','Auto')),  -- Auto = absent: didn't check in before the end
    MarkedAt   DATETIME2    NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT PK_TutorialAttendance PRIMARY KEY (TutorialId, StudentId)
);

CREATE TABLE dbo.ClassRegisters (
    RegisterId   INT IDENTITY(1,1) PRIMARY KEY,
    TutorId      INT           NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId     INT           NOT NULL REFERENCES dbo.Modules(ModuleId),
    YearOfStudy  INT           NOT NULL,
    Title        NVARCHAR(150) NOT NULL,
    RegisterDate DATE          NOT NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TABLE dbo.ClassRegisterAttendance (
    RegisterId INT          NOT NULL REFERENCES dbo.ClassRegisters(RegisterId),
    StudentId  INT          NOT NULL REFERENCES dbo.Users(UserId),
    Status     NVARCHAR(10) NOT NULL CONSTRAINT CK_CRA_Status CHECK (Status IN ('Present','Late','Absent')),
    MarkedAt   DATETIME2    NOT NULL DEFAULT dbo.fn_SaNow(),
    CONSTRAINT PK_ClassRegisterAttendance PRIMARY KEY (RegisterId, StudentId)
);

/* ---------- Content & communication ---------- */
CREATE TABLE dbo.CourseContent (
    ContentId    INT IDENTITY(1,1) PRIMARY KEY,
    ModuleId     INT            NOT NULL REFERENCES dbo.Modules(ModuleId),
    YearOfStudy  INT            NULL,
    Title        NVARCHAR(150)  NOT NULL,
    Description  NVARCHAR(1000) NULL,
    FileName     NVARCHAR(260)  NOT NULL,
    FilePath     NVARCHAR(400)  NOT NULL,
    UploadedBy   INT            NULL REFERENCES dbo.Users(UserId),
    UploaderRole NVARCHAR(20)   NOT NULL,
    UploadedAt   DATETIME2      NOT NULL DEFAULT dbo.fn_SaNow(),
    IsReviewed   BIT            NOT NULL DEFAULT 0,
    ReviewedBy   INT            NULL REFERENCES dbo.Users(UserId),
    ReviewedAt   DATETIME2      NULL
);

CREATE TABLE dbo.Announcements (
    AnnouncementId INT IDENTITY(1,1) PRIMARY KEY,
    TutorId        INT            NOT NULL REFERENCES dbo.Users(UserId),
    ModuleId       INT            NOT NULL REFERENCES dbo.Modules(ModuleId),
    YearOfStudy    INT            NOT NULL,
    Title          NVARCHAR(150)  NOT NULL,
    Message        NVARCHAR(2000) NOT NULL,
    CreatedAt      DATETIME2      NOT NULL DEFAULT dbo.fn_SaNow()
);

/* ---------- Tutor applications ---------- */
CREATE TABLE dbo.TutorApplications (
    ApplicationId          INT IDENTITY(1,1) PRIMARY KEY,
    StudentId              INT            NULL REFERENCES dbo.Users(UserId),
    FirstName              NVARCHAR(60)   NOT NULL,
    LastName               NVARCHAR(60)   NOT NULL,
    StudentNumber          NVARCHAR(20)   NOT NULL,
    Email                  NVARCHAR(150)  NOT NULL,   -- university email, becomes the tutor login
    PersonalEmail          NVARCHAR(150)  NOT NULL,   -- where login details are sent
    Phone                  NVARCHAR(20)   NULL,
    CourseId               INT            NULL REFERENCES dbo.Courses(CourseId),
    CurrentYear            INT            NOT NULL,
    ModuleId               INT            NOT NULL REFERENCES dbo.Modules(ModuleId),
    TutorYear              INT            NOT NULL,
    ModuleMark             DECIMAL(5,2)   NOT NULL,
    Motivation             NVARCHAR(2000) NOT NULL,
    IdDocumentPath         NVARCHAR(400)  NOT NULL,
    TranscriptPath         NVARCHAR(400)  NULL,
    CvPath                 NVARCHAR(400)  NULL,
    Status                 NVARCHAR(20)   NOT NULL DEFAULT 'Submitted'
                           CONSTRAINT CK_App_Status CHECK (Status IN ('Submitted','UnderReview','Recommended','NotRecommended','Approved','Rejected')),
    AssignedLecturerId     INT            NULL REFERENCES dbo.Users(UserId),
    LecturerRecommendation NVARCHAR(10)   NULL,
    LecturerComment        NVARCHAR(1000) NULL,
    RecommendedAt          DATETIME2      NULL,
    AdminFeedback          NVARCHAR(1000) NULL,
    SubmittedAt            DATETIME2      NOT NULL DEFAULT dbo.fn_SaNow(),
    DecidedAt              DATETIME2      NULL
);

/* ---------- Support tables ---------- */
CREATE TABLE dbo.PasswordResetTokens (
    Token     NVARCHAR(64) PRIMARY KEY,
    UserId    INT          NOT NULL REFERENCES dbo.Users(UserId),
    ExpiresAt DATETIME2    NOT NULL,
    Used      BIT          NOT NULL DEFAULT 0
);

/* Notes between the administrator and the reviewing lecturer on a tutor application. */
CREATE TABLE dbo.ApplicationComments (
    CommentId     INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationId INT            NOT NULL REFERENCES dbo.TutorApplications(ApplicationId),
    UserId        INT            NOT NULL REFERENCES dbo.Users(UserId),
    Role          NVARCHAR(20)   NOT NULL,
    Comment       NVARCHAR(1000) NOT NULL,
    CreatedAt     DATETIME2      NOT NULL DEFAULT dbo.fn_SaNow()
);
CREATE INDEX IX_ApplicationComments_App ON dbo.ApplicationComments (ApplicationId, CreatedAt);
CREATE UNIQUE INDEX UX_Tutorials_CheckInCode ON dbo.Tutorials (CheckInCode) WHERE CheckInCode IS NOT NULL;

/* Personal access tokens for the REST API. Only a SHA-256 hash of each token is stored. */
CREATE TABLE dbo.ApiTokens (
    TokenId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT           NOT NULL REFERENCES dbo.Users(UserId),
    TokenHash  NVARCHAR(64)  NOT NULL CONSTRAINT UQ_ApiTokens_Hash UNIQUE,
    Name       NVARCHAR(60)  NOT NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
    ExpiresAt  DATETIME2     NOT NULL,
    LastUsedAt DATETIME2     NULL,
    RevokedAt  DATETIME2     NULL
);
CREATE INDEX IX_ApiTokens_User ON dbo.ApiTokens (UserId);

/* AI study assistant usage, for rate limiting. The question and answer themselves are never stored (POPIA). */
CREATE TABLE dbo.AiRequests (
    RequestId  INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT           NOT NULL REFERENCES dbo.Users(UserId),
    Mode       NVARCHAR(30)  NOT NULL,
    ModuleCode NVARCHAR(20)  NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);
CREATE INDEX IX_AiRequests_User ON dbo.AiRequests (UserId, CreatedAt);

CREATE TABLE dbo.ActivityLog (
    ActivityId INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT           NULL REFERENCES dbo.Users(UserId),
    Message    NVARCHAR(400) NOT NULL,
    Link       NVARCHAR(300) NULL,          -- page the notification opens (null = worked out from the message)
    CreatedAt  DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
);

CREATE TYPE dbo.ModuleListType AS TABLE (
    Code       NVARCHAR(20)  NOT NULL,
    Name       NVARCHAR(150) NOT NULL,
    YearLevel  INT           NOT NULL,
    Semester   INT           NOT NULL DEFAULT 1,
    LecturerId INT           NULL
);
GO

/* ---------- Indexes ---------- */
CREATE INDEX IX_Bookings_Student ON dbo.Bookings(StudentId, StartTime);
CREATE INDEX IX_Bookings_Tutor   ON dbo.Bookings(TutorId, StartTime);
CREATE INDEX IX_Slots_Tutor      ON dbo.AvailabilitySlots(TutorId, StartTime);
CREATE INDEX IX_Tutorials_Module ON dbo.Tutorials(ModuleId, YearOfStudy, StartTime);
CREATE INDEX IX_Activity_User    ON dbo.ActivityLog(UserId, CreatedAt);
CREATE INDEX IX_Payments_Status  ON dbo.Payments(Status, CreatedAt);
GO
PRINT 'TutorConnect schema created.';
GO

/* ---------- Performance ----------
   SQL Server Express turns AUTO_CLOSE on by default: the database shuts down when the last connection closes and
   has to start again on the next request, which makes pages slow. Keep it open, and index the common lookups. */
IF DATABASEPROPERTYEX(DB_NAME(), 'IsAutoClose') = 1
BEGIN
    DECLARE @sql NVARCHAR(200) = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET AUTO_CLOSE OFF';
    EXEC (@sql);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentModules_Module') CREATE INDEX IX_StudentModules_Module ON dbo.StudentModules (ModuleId) INCLUDE (StudentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TutorModules_Tutor') CREATE INDEX IX_TutorModules_Tutor ON dbo.TutorModules (TutorId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TutorModules_Module') CREATE INDEX IX_TutorModules_Module ON dbo.TutorModules (ModuleId, YearOfStudy);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TutorialAttendance_Student') CREATE INDEX IX_TutorialAttendance_Student ON dbo.TutorialAttendance (StudentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassRegisterAttendance_Student') CREATE INDEX IX_ClassRegisterAttendance_Student ON dbo.ClassRegisterAttendance (StudentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Student') CREATE INDEX IX_Payments_Student ON dbo.Payments (StudentId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Tutor') CREATE INDEX IX_Payments_Tutor ON dbo.Payments (TutorId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payouts_Payment') CREATE INDEX IX_Payouts_Payment ON dbo.Payouts (PaymentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Modules_Lecturer') CREATE INDEX IX_Modules_Lecturer ON dbo.Modules (LecturerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseModules_Module') CREATE INDEX IX_CourseModules_Module ON dbo.CourseModules (ModuleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Role') CREATE INDEX IX_Users_Role ON dbo.Users (Role, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Course') CREATE INDEX IX_Users_Course ON dbo.Users (CourseId, YearOfStudy);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Courses_Department') CREATE INDEX IX_Courses_Department ON dbo.Courses (DepartmentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TutorApplications_Email') CREATE INDEX IX_TutorApplications_Email ON dbo.TutorApplications (Email);
GO
