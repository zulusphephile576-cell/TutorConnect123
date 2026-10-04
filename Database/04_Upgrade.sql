/* =====================================================================
   TutorConnect - 04_Upgrade.sql
   Brings an existing database up to date WITHOUT deleting any data:
   adds columns introduced in later versions. Run it, then run
   02_StoredProcedures.sql again (that script is always safe to re-run).
   The app's /Setup page and startup check do both of these automatically.
   ===================================================================== */
USE TutorConnect;
GO
/* ---------- South African time ----------
   Every "now" in TutorConnect is South African Standard Time (UTC+2), whatever time zone the server uses. */
CREATE OR ALTER FUNCTION dbo.fn_SaNow() RETURNS DATETIME2 AS
BEGIN
    RETURN CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'South Africa Standard Time' AS DATETIME2);
END
GO

IF COL_LENGTH('dbo.Users', 'NotificationsSeenAt') IS NULL ALTER TABLE dbo.Users ADD NotificationsSeenAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Users', 'DateOfBirth') IS NULL ALTER TABLE dbo.Users ADD DateOfBirth DATE NULL;
IF COL_LENGTH('dbo.Users', 'Gender') IS NULL ALTER TABLE dbo.Users ADD Gender NVARCHAR(30) NULL;
IF COL_LENGTH('dbo.Users', 'Campus') IS NULL ALTER TABLE dbo.Users ADD Campus NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.Users', 'EmergencyContactName') IS NULL ALTER TABLE dbo.Users ADD EmergencyContactName NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.Users', 'EmergencyContactPhone') IS NULL ALTER TABLE dbo.Users ADD EmergencyContactPhone NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Users', 'PopiaConsentAt') IS NULL ALTER TABLE dbo.Users ADD PopiaConsentAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Users', 'DeletionRequestedAt') IS NULL ALTER TABLE dbo.Users ADD DeletionRequestedAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Users', 'FailedLoginCount') IS NULL ALTER TABLE dbo.Users ADD FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT 0;
IF COL_LENGTH('dbo.Users', 'LockoutUntil') IS NULL ALTER TABLE dbo.Users ADD LockoutUntil DATETIME2 NULL;
IF OBJECT_ID('dbo.ApiTokens') IS NULL
BEGIN
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
END
GO
IF OBJECT_ID('dbo.Faculties') IS NULL
    CREATE TABLE dbo.Faculties (
        FacultyId    INT IDENTITY(1,1) PRIMARY KEY,
        Name         NVARCHAR(120) NOT NULL CONSTRAINT UQ_Faculties_Name UNIQUE,
        CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
    );
IF OBJECT_ID('dbo.Departments') IS NULL
    CREATE TABLE dbo.Departments (
        DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
        FacultyId    INT           NOT NULL REFERENCES dbo.Faculties(FacultyId),
        Name         NVARCHAR(120) NOT NULL,
        CreatedAt    DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow(),
        CONSTRAINT UQ_Departments_Faculty_Name UNIQUE (FacultyId, Name)
    );
IF COL_LENGTH('dbo.Courses', 'DepartmentId') IS NULL
    ALTER TABLE dbo.Courses ADD DepartmentId INT NULL REFERENCES dbo.Departments(DepartmentId);
IF OBJECT_ID('dbo.AiRequests') IS NULL
BEGIN
    CREATE TABLE dbo.AiRequests (
        RequestId  INT IDENTITY(1,1) PRIMARY KEY,
        UserId     INT           NOT NULL REFERENCES dbo.Users(UserId),
        Mode       NVARCHAR(30)  NOT NULL,
        ModuleCode NVARCHAR(20)  NULL,
        CreatedAt  DATETIME2     NOT NULL DEFAULT dbo.fn_SaNow()
    );
    CREATE INDEX IX_AiRequests_User ON dbo.AiRequests (UserId, CreatedAt);
END
GO
/* ---------- Faculties and departments (Durban University of Technology). Only added if missing. ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Accounting and Informatics');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Information Technology')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Information Technology' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Information Systems')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Information Systems' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Information and Corporate Management')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Information and Corporate Management' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Financial Accounting')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Financial Accounting' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Management Accounting')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Management Accounting' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Auditing and Taxation')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Auditing and Taxation' FROM dbo.Faculties WHERE Name = N'Faculty of Accounting and Informatics';
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Applied Sciences');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Biotechnology and Food Science')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Biotechnology and Food Science' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Chemistry')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Chemistry' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Horticulture')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Horticulture' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Maritime Studies')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Maritime Studies' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Sport Studies')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Sport Studies' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Applied Sciences' AND d.Name = N'Textile Science')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Textile Science' FROM dbo.Faculties WHERE Name = N'Faculty of Applied Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Arts and Design');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Arts and Design' AND d.Name = N'Fashion and Textiles')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Fashion and Textiles' FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Arts and Design' AND d.Name = N'Media, Language and Communication')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Media, Language and Communication' FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Arts and Design' AND d.Name = N'Video Technology')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Video Technology' FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Arts and Design' AND d.Name = N'Visual Communication Design')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Visual Communication Design' FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Arts and Design' AND d.Name = N'Drama and Production Studies')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Drama and Production Studies' FROM dbo.Faculties WHERE Name = N'Faculty of Arts and Design';
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Engineering and the Built Environment');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Engineering and the Built Environment' AND d.Name = N'Civil Engineering')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Civil Engineering' FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Engineering and the Built Environment' AND d.Name = N'Electrical Power Engineering')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Electrical Power Engineering' FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Engineering and the Built Environment' AND d.Name = N'Mechanical Engineering')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Mechanical Engineering' FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Engineering and the Built Environment' AND d.Name = N'Chemical Engineering')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Chemical Engineering' FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Engineering and the Built Environment' AND d.Name = N'Architecture')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Architecture' FROM dbo.Faculties WHERE Name = N'Faculty of Engineering and the Built Environment';
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Health Sciences');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Health Sciences' AND d.Name = N'Nursing')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Nursing' FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Health Sciences' AND d.Name = N'Emergency Medical Care and Rescue')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Emergency Medical Care and Rescue' FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Health Sciences' AND d.Name = N'Dental Sciences')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Dental Sciences' FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Health Sciences' AND d.Name = N'Somatology')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Somatology' FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Health Sciences' AND d.Name = N'Radiography')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Radiography' FROM dbo.Faculties WHERE Name = N'Faculty of Health Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences') INSERT dbo.Faculties (Name) VALUES (N'Faculty of Management Sciences');
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Management Sciences' AND d.Name = N'Marketing and Retail')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Marketing and Retail' FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Management Sciences' AND d.Name = N'Human Resources Management')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Human Resources Management' FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Management Sciences' AND d.Name = N'Hospitality and Tourism')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Hospitality and Tourism' FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Management Sciences' AND d.Name = N'Public Management and Economics')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Public Management and Economics' FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences';
IF NOT EXISTS (SELECT 1 FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId WHERE f.Name = N'Faculty of Management Sciences' AND d.Name = N'Operations and Quality Management')
    INSERT dbo.Departments (FacultyId, Name) SELECT FacultyId, N'Operations and Quality Management' FROM dbo.Faculties WHERE Name = N'Faculty of Management Sciences';

-- Courses created before faculties existed are placed in Information Technology; move them under Admin, Courses.
UPDATE dbo.Courses SET DepartmentId = (SELECT d.DepartmentId FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
                                       WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Information Technology')
WHERE DepartmentId IS NULL;
GO
/* ---------- Several tutors per module, lecturer's target, tutorial check-in codes, application notes ---------- */
IF COL_LENGTH('dbo.Modules', 'TutorsNeeded') IS NULL
    ALTER TABLE dbo.Modules ADD TutorsNeeded INT NOT NULL CONSTRAINT DF_Modules_TutorsNeeded DEFAULT 1;
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_TutorModules' AND parent_object_id = OBJECT_ID('dbo.TutorModules'))
    ALTER TABLE dbo.TutorModules DROP CONSTRAINT UQ_TutorModules;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_TutorModules_Tutor')
    ALTER TABLE dbo.TutorModules ADD CONSTRAINT UQ_TutorModules_Tutor UNIQUE (TutorId, ModuleId, YearOfStudy);
IF COL_LENGTH('dbo.Tutorials', 'CheckInCode') IS NULL
    ALTER TABLE dbo.Tutorials ADD CheckInCode NVARCHAR(8) NULL;
IF OBJECT_ID('dbo.ApplicationComments') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationComments (
        CommentId     INT IDENTITY(1,1) PRIMARY KEY,
        ApplicationId INT            NOT NULL REFERENCES dbo.TutorApplications(ApplicationId),
        UserId        INT            NOT NULL REFERENCES dbo.Users(UserId),
        Role          NVARCHAR(20)   NOT NULL,
        Comment       NVARCHAR(1000) NOT NULL,
        CreatedAt     DATETIME2      NOT NULL DEFAULT dbo.fn_SaNow()
    );
    CREATE INDEX IX_ApplicationComments_App ON dbo.ApplicationComments (ApplicationId, CreatedAt);
END
GO
-- Give every existing tutorial its own check-in code.
DECLARE @alphabet NCHAR(31) = N'ABCDEFGHJKLMNPQRSTUVWXYZ2345679';
DECLARE @id INT, @code NVARCHAR(8), @i INT;
WHILE EXISTS (SELECT 1 FROM dbo.Tutorials WHERE CheckInCode IS NULL)
BEGIN
    SELECT TOP 1 @id = TutorialId FROM dbo.Tutorials WHERE CheckInCode IS NULL;
    SET @code = N''; SET @i = 0;
    WHILE @i < 6 BEGIN SET @code += SUBSTRING(@alphabet, 1 + ABS(CHECKSUM(NEWID())) % 31, 1); SET @i += 1; END
    IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials WHERE CheckInCode = @code)
        UPDATE dbo.Tutorials SET CheckInCode = @code WHERE TutorialId = @id;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Tutorials_CheckInCode')
    CREATE UNIQUE INDEX UX_Tutorials_CheckInCode ON dbo.Tutorials (CheckInCode) WHERE CheckInCode IS NOT NULL;
-- attendance can now be marked automatically ('Auto' = absent because the student didn't check in)
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TA_Method' AND definition NOT LIKE '%Auto%')
BEGIN
    ALTER TABLE dbo.TutorialAttendance DROP CONSTRAINT CK_TA_Method;
    ALTER TABLE dbo.TutorialAttendance ADD CONSTRAINT CK_TA_Method CHECK (Method IN ('Manual','QR','Auto'));
END
IF COL_LENGTH('dbo.ActivityLog', 'Link') IS NULL ALTER TABLE dbo.ActivityLog ADD Link NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.TutorApplications', 'CvPath') IS NULL ALTER TABLE dbo.TutorApplications ADD CvPath NVARCHAR(400) NULL;
IF COL_LENGTH('dbo.Modules', 'Semester') IS NULL
    ALTER TABLE dbo.Modules ADD Semester INT NOT NULL CONSTRAINT DF_Modules_Semester DEFAULT 1
        CONSTRAINT CK_Modules_Semester CHECK (Semester IN (1, 2));
IF COL_LENGTH('dbo.Payments', 'PaymentMethod') IS NULL ALTER TABLE dbo.Payments ADD PaymentMethod NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Payments', 'PayerBank') IS NULL ALTER TABLE dbo.Payments ADD PayerBank NVARCHAR(60) NULL;
IF COL_LENGTH('dbo.Payments', 'PayerAccountLast4') IS NULL ALTER TABLE dbo.Payments ADD PayerAccountLast4 NVARCHAR(4) NULL;
-- The bulk module list gains Semester and LecturerId. A table type can't be altered, so rebuild it (and the procedure using it,
-- which 02_StoredProcedures.sql re-creates).
IF NOT EXISTS (SELECT 1 FROM sys.table_types tt JOIN sys.columns c ON c.object_id = tt.type_table_object_id
               WHERE tt.name = 'ModuleListType' AND c.name = 'LecturerId')
BEGIN
    IF OBJECT_ID('dbo.sp_Course_CreateWithModules') IS NOT NULL DROP PROCEDURE dbo.sp_Course_CreateWithModules;
    IF OBJECT_ID('dbo.sp_Module_BulkCreate') IS NOT NULL DROP PROCEDURE dbo.sp_Module_BulkCreate;
    IF TYPE_ID('dbo.ModuleListType') IS NOT NULL DROP TYPE dbo.ModuleListType;
    EXEC (N'CREATE TYPE dbo.ModuleListType AS TABLE (
        Code NVARCHAR(20) NOT NULL, Name NVARCHAR(150) NOT NULL, YearLevel INT NOT NULL,
        Semester INT NOT NULL DEFAULT 1, LecturerId INT NULL);');
END
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
PRINT 'TutorConnect schema upgraded. Now run 02_StoredProcedures.sql.';
