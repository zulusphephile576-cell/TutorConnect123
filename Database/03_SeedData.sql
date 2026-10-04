/* =====================================================================
   TutorConnect - 03_SeedData.sql
   Demo data so every role can be explored immediately.

   Logins (choose the matching tab on the login page):
     Admin     admin@tutorconnect.ac.za      Admin@123
     Lecturer  lecturer@tutorconnect.ac.za   Password@123
     Tutor     tutor@tutorconnect.ac.za      Password@123
     Student   student@tutorconnect.ac.za    Password@123   (also: liam@..., ayesha@...)

   Note: in normal operation tutors are only ever created by approving an
   application. The demo tutor below exists purely so the app has data to show.
   ===================================================================== */
USE TutorConnect;
GO
SET NOCOUNT ON;

DECLARE @AdminHash NVARCHAR(100) = N'$2b$11$00ViAmqeUgGIcVZ32GO.5.Z/JL7b//jhOkbUgp9Jp2yJNfUtTB4Fq';
DECLARE @Hash      NVARCHAR(100) = N'$2b$11$L7kVZX1ksoohWw3MfVBEhO5qearKipQ7qnn0ADTN5QMCymnSFI6ha';

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

DECLARE @DeptIT INT = (SELECT d.DepartmentId FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
                       WHERE f.Name = N'Faculty of Accounting and Informatics' AND d.Name = N'Information Technology');
INSERT dbo.Courses (Code, Name, Description, DepartmentId) VALUES
 (N'BSCIT', N'BSc Information Technology', N'Three-year degree in software development, data and networks.', @DeptIT),
 (N'DICT',  N'Diploma in ICT: Applications Development', N'Three-year diploma focused on building business applications.', @DeptIT);

-- A few courses in other faculties so the faculty, department and course lists can be tried out.
INSERT dbo.Courses (Code, Name, Description, DepartmentId)
SELECT v.Code, v.Name, v.Description, d.DepartmentId
FROM (VALUES
    (N'DIPACC', N'Diploma in Accounting', N'Financial accounting, tax and auditing foundations.', N'Faculty of Accounting and Informatics', N'Financial Accounting'),
    (N'BENGCE', N'Bachelor of Engineering Technology in Civil Engineering', N'Structures, water and transportation engineering.', N'Faculty of Engineering and the Built Environment', N'Civil Engineering'),
    (N'BNURS',  N'Bachelor of Nursing', N'Four-year professional nursing degree.', N'Faculty of Health Sciences', N'Nursing'),
    (N'DIPMKT', N'Diploma in Marketing', N'Marketing, retail and consumer behaviour.', N'Faculty of Management Sciences', N'Marketing and Retail'),
    (N'DIPBIO', N'Diploma in Biotechnology', N'Laboratory science, microbiology and bioprocessing.', N'Faculty of Applied Sciences', N'Biotechnology and Food Science'),
    (N'DIPGD',  N'Diploma in Visual Communication Design', N'Graphic design, typography and digital media.', N'Faculty of Arts and Design', N'Visual Communication Design')
) v(Code, Name, Description, Faculty, Department)
JOIN dbo.Faculties f ON f.Name = v.Faculty
JOIN dbo.Departments d ON d.FacultyId = f.FacultyId AND d.Name = v.Department;

INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, Phone) VALUES
 (N'System', N'Administrator', N'admin@tutorconnect.ac.za', @AdminHash, 'Admin', N'0310000000');
INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, Phone, PersonalEmail) VALUES
 (N'Nomvula', N'Dlamini', N'lecturer@tutorconnect.ac.za', @Hash, 'Lecturer', N'0311234567', N'nomvula.dlamini@example.com');

DECLARE @Lecturer INT = (SELECT UserId FROM dbo.Users WHERE Email = N'lecturer@tutorconnect.ac.za');
DECLARE @BSCIT INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'BSCIT');
DECLARE @DICT  INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'DICT');

INSERT dbo.Modules (Code, Name, YearLevel, Description, LecturerId) VALUES
 (N'ITPR101', N'Programming 1',            1, N'Introduction to programming in C#.', @Lecturer),
 (N'ITDB101', N'Database Fundamentals',    1, N'Relational modelling and SQL.', @Lecturer),
 (N'ITWD101', N'Web Development 1',        1, N'HTML, CSS and client-side scripting.', @Lecturer),
 (N'ITPR201', N'Programming 2',            2, N'Object-oriented programming and data structures.', @Lecturer),
 (N'ITDB201', N'Database Systems',         2, N'Stored procedures, transactions and design.', @Lecturer),
 (N'ITSE201', N'Software Engineering',     2, N'Requirements, design and testing.', NULL),
 (N'ITPJ301', N'Capstone Project',         3, N'Team-based software project.', NULL),
 (N'ITNS301', N'Network Security',         3, N'Security principles and practice.', NULL);

INSERT dbo.CourseModules (CourseId, ModuleId) SELECT @BSCIT, ModuleId FROM dbo.Modules;
INSERT dbo.CourseModules (CourseId, ModuleId) SELECT @DICT, ModuleId FROM dbo.Modules WHERE Code IN (N'ITPR101', N'ITDB101', N'ITWD101', N'ITPR201');

INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, StudentNumber, Phone, CourseId, YearOfStudy, CreatedAt) VALUES
 (N'Thandi', N'Mokoena', N'student@tutorconnect.ac.za', @Hash, 'Student', N'22012345', N'0821112222', @BSCIT, 1, DATEADD(MONTH, -2, dbo.fn_SaNow())),
 (N'Liam',   N'Naidoo',  N'liam@tutorconnect.ac.za',    @Hash, 'Student', N'22019876', N'0823334444', @BSCIT, 1, DATEADD(MONTH, -1, dbo.fn_SaNow())),
 (N'Ayesha', N'Khan',    N'ayesha@tutorconnect.ac.za',  @Hash, 'Student', N'21015555', N'0825556666', @BSCIT, 2, DATEADD(MONTH, -4, dbo.fn_SaNow())),
 (N'Sipho',  N'Ndlovu',  N'tutor@tutorconnect.ac.za',   @Hash, 'Tutor',   N'20011111', N'0827778888', @BSCIT, 3, DATEADD(MONTH, -3, dbo.fn_SaNow()));

UPDATE dbo.Users SET PersonalEmail = N'sipho.ndlovu@example.com' WHERE Email = N'tutor@tutorconnect.ac.za';

INSERT dbo.StudentModules (StudentId, ModuleId)
SELECT u.UserId, cm.ModuleId FROM dbo.Users u
JOIN dbo.CourseModules cm ON cm.CourseId = u.CourseId JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId AND m.YearLevel = u.YearOfStudy
WHERE u.Role = 'Student';

DECLARE @Tutor INT = (SELECT UserId FROM dbo.Users WHERE Email = N'tutor@tutorconnect.ac.za');
INSERT dbo.TutorModules (TutorId, ModuleId, YearOfStudy, AssignedBy)
SELECT @Tutor, ModuleId, YearLevel, @Lecturer FROM dbo.Modules WHERE Code IN (N'ITPR101', N'ITDB101', N'ITPR201');

-- Open slots over the next few days (09:00-12:00 and 14:00-16:00)
DECLARE @d INT = 1;
WHILE @d <= 5
BEGIN
    DECLARE @Day DATETIME2 = DATEADD(DAY, @d, CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2));
    INSERT dbo.AvailabilitySlots (TutorId, StartTime, EndTime) VALUES
      (@Tutor, DATEADD(HOUR, 9, @Day), DATEADD(HOUR, 12, @Day)),
      (@Tutor, DATEADD(HOUR, 14, @Day), DATEADD(HOUR, 16, @Day));
    SET @d += 1;
END

DECLARE @PR101 INT = (SELECT ModuleId FROM dbo.Modules WHERE Code = N'ITPR101');
INSERT dbo.Tutorials (TutorId, ModuleId, YearOfStudy, Title, Description, Venue, StartTime, EndTime) VALUES
 (@Tutor, @PR101, 1, N'Loops and arrays revision', N'Bring your laptop. We will work through past test questions on loops and arrays.', N'Lab 3, S Block',
  DATEADD(HOUR, 10, DATEADD(DAY, 2, CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2))), DATEADD(HOUR, 12, DATEADD(DAY, 2, CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2)))),
 (@Tutor, @PR101, 1, N'Methods and parameters', N'Worked examples on writing and calling methods.', N'Lab 3, S Block',
  DATEADD(HOUR, 10, DATEADD(DAY, -5, CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2))), DATEADD(HOUR, 12, DATEADD(DAY, -5, CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2))));

DECLARE @PastTutorial INT = (SELECT TutorialId FROM dbo.Tutorials WHERE Title = N'Methods and parameters');
INSERT dbo.TutorialAttendance (TutorialId, StudentId, Status, Method)
SELECT @PastTutorial, UserId, CASE WHEN Email LIKE N'student@%' THEN 'Present' ELSE 'Late' END, 'QR'
FROM dbo.Users WHERE Role = 'Student' AND YearOfStudy = 1;

INSERT dbo.Announcements (TutorId, ModuleId, YearOfStudy, Title, Message) VALUES
 (@Tutor, @PR101, 1, N'Test 1 prep', N'I have opened extra one-on-one slots this week before Test 1. Book early, they fill up fast.');

INSERT dbo.ActivityLog (UserId, Message)
SELECT UserId, N'Welcome to TutorConnect.' FROM dbo.Users;
GO

GO
-- Every tutorial gets its own short check-in code (students can type it instead of scanning).
DECLARE @alphabet NCHAR(31) = N'ABCDEFGHJKLMNPQRSTUVWXYZ2345679';
DECLARE @tid INT, @code NVARCHAR(8), @n INT;
WHILE EXISTS (SELECT 1 FROM dbo.Tutorials WHERE CheckInCode IS NULL)
BEGIN
    SELECT TOP 1 @tid = TutorialId FROM dbo.Tutorials WHERE CheckInCode IS NULL;
    SET @code = N''; SET @n = 0;
    WHILE @n < 6 BEGIN SET @code += SUBSTRING(@alphabet, 1 + ABS(CHECKSUM(NEWID())) % 31, 1); SET @n += 1; END
    IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials WHERE CheckInCode = @code) UPDATE dbo.Tutorials SET CheckInCode = @code WHERE TutorialId = @tid;
END
UPDATE dbo.Modules SET TutorsNeeded = 2 WHERE Code IN (N'ITPR101', N'ITDB101');
GO
/* ---------- More demo people (same as 05_MoreDemoUsers.sql) ---------- */
SET NOCOUNT ON;

DECLARE @Hash NVARCHAR(100) = N'$2b$11$L7kVZX1ksoohWw3MfVBEhO5qearKipQ7qnn0ADTN5QMCymnSFI6ha';   -- Password@123
DECLARE @BSCIT INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'BSCIT');
DECLARE @DICT  INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'DICT');
IF @BSCIT IS NULL OR @DICT IS NULL
    THROW 50900, N'The BSCIT and DICT courses are needed.', 1;

DECLARE @People TABLE (
    FirstName NVARCHAR(60), LastName NVARCHAR(60), Email NVARCHAR(150), Role NVARCHAR(20),
    StudentNumber NVARCHAR(20) NULL, Phone NVARCHAR(20), CourseCode NVARCHAR(20) NULL, YearOfStudy INT NULL,
    DateOfBirth DATE NULL, Gender NVARCHAR(30) NULL, Campus NVARCHAR(80) NULL, PersonalEmail NVARCHAR(150) NULL, JoinedMonthsAgo INT);

INSERT @People VALUES
 -- administrator and lecturers
 (N'Zanele',    N'Khumalo',       N'zanele.khumalo@tutorconnect.ac.za',     'Admin',    NULL, N'0312735000', NULL, NULL, NULL, N'Female', NULL, NULL, 10),
 (N'Sibusiso',  N'Mthembu',       N'sibusiso.mthembu@tutorconnect.ac.za',   'Lecturer', NULL, N'0312735101', NULL, NULL, NULL, N'Male',   NULL, N'sibusiso.mthembu@example.com', 9),
 (N'Priya',     N'Govender',      N'priya.govender@tutorconnect.ac.za',     'Lecturer', NULL, N'0312735102', NULL, NULL, NULL, N'Female', NULL, N'priya.govender@example.com', 9),
 (N'Johan',     N'van der Merwe', N'johan.vandermerwe@tutorconnect.ac.za',  'Lecturer', NULL, N'0312735103', NULL, NULL, NULL, N'Male',   NULL, N'johan.vdm@example.com', 8),
 -- tutors (senior students)
 (N'Zinhle',    N'Mkhize',        N'zinhle.mkhize@tutorconnect.ac.za',      'Tutor', N'20014521', N'0731234501', N'BSCIT', 3, '2003-04-11', N'Female', N'Steve Biko Campus', N'zinhle.mkhize@example.com', 7),
 (N'Kabelo',    N'Molefe',        N'kabelo.molefe@tutorconnect.ac.za',      'Tutor', N'20017734', N'0731234502', N'BSCIT', 3, '2002-09-23', N'Male',   N'Steve Biko Campus', N'kabelo.molefe@example.com', 7),
 (N'Ruan',      N'Pretorius',     N'ruan.pretorius@tutorconnect.ac.za',     'Tutor', N'19021187', N'0731234503', N'BSCIT', 3, '2001-12-02', N'Male',   N'ML Sultan Campus',  N'ruan.pretorius@example.com', 6),
 (N'Nadia',     N'Pillay',        N'nadia.pillay@tutorconnect.ac.za',       'Tutor', N'20016645', N'0731234504', N'BSCIT', 3, '2003-01-30', N'Female', N'Ritson Campus',     N'nadia.pillay@example.com', 6),
 (N'Musa',      N'Zungu',         N'musa.zungu@tutorconnect.ac.za',         'Tutor', N'21011902', N'0731234505', N'DICT',  2, '2004-06-17', N'Male',   N'Steve Biko Campus', N'musa.zungu@example.com', 5),
 (N'Lerato',    N'Mahlangu',      N'lerato.mahlangu@tutorconnect.ac.za',    'Tutor', N'19024418', N'0731234506', N'BSCIT', 3, '2001-07-08', N'Female', N'ML Sultan Campus',  N'lerato.mahlangu@example.com', 5),
 -- BSc IT, year 1
 (N'Ayanda',    N'Ngcobo',        N'ayanda.ngcobo@tutorconnect.ac.za',      'Student', N'22031001', N'0611000001', N'BSCIT', 1, '2006-02-14', N'Female', N'Steve Biko Campus', NULL, 4),
 (N'Bongani',   N'Sithole',       N'bongani.sithole@tutorconnect.ac.za',    'Student', N'22031002', N'0611000002', N'BSCIT', 1, '2005-11-03', N'Male',   N'Steve Biko Campus', NULL, 4),
 (N'Chloe',     N'Daniels',       N'chloe.daniels@tutorconnect.ac.za',      'Student', N'22031003', N'0611000003', N'BSCIT', 1, '2006-05-21', N'Female', N'ML Sultan Campus',  NULL, 3),
 (N'Dumisani',  N'Cele',          N'dumisani.cele@tutorconnect.ac.za',      'Student', N'22031004', N'0611000004', N'BSCIT', 1, '2005-08-09', N'Male',   N'Steve Biko Campus', NULL, 3),
 (N'Kayla',     N'Reddy',         N'kayla.reddy@tutorconnect.ac.za',        'Student', N'22031005', N'0611000005', N'BSCIT', 1, '2006-01-27', N'Female', N'Ritson Campus',     NULL, 3),
 (N'Lwazi',     N'Dube',          N'lwazi.dube@tutorconnect.ac.za',         'Student', N'22031006', N'0611000006', N'BSCIT', 1, '2005-12-12', N'Male',   N'Steve Biko Campus', NULL, 2),
 (N'Mpho',      N'Tshabalala',    N'mpho.tshabalala@tutorconnect.ac.za',    'Student', N'22031007', N'0611000007', N'BSCIT', 1, '2006-03-30', N'Female', N'ML Sultan Campus',  NULL, 2),
 (N'Ntando',    N'Gumede',        N'ntando.gumede@tutorconnect.ac.za',      'Student', N'22031008', N'0611000008', N'BSCIT', 1, '2005-10-05', N'Male',   N'Steve Biko Campus', NULL, 1),
 -- BSc IT, year 2
 (N'Amahle',    N'Zulu',          N'amahle.zulu@tutorconnect.ac.za',        'Student', N'21025001', N'0621000001', N'BSCIT', 2, '2005-04-02', N'Female', N'Steve Biko Campus', NULL, 9),
 (N'Ethan',     N'Moodley',       N'ethan.moodley@tutorconnect.ac.za',      'Student', N'21025002', N'0621000002', N'BSCIT', 2, '2004-07-19', N'Male',   N'Ritson Campus',     NULL, 9),
 (N'Hlengiwe',  N'Buthelezi',     N'hlengiwe.buthelezi@tutorconnect.ac.za', 'Student', N'21025003', N'0621000003', N'BSCIT', 2, '2004-12-25', N'Female', N'Steve Biko Campus', NULL, 8),
 (N'Jason',     N'Botha',         N'jason.botha@tutorconnect.ac.za',        'Student', N'21025004', N'0621000004', N'BSCIT', 2, '2005-02-08', N'Male',   N'ML Sultan Campus',  NULL, 8),
 (N'Naledi',    N'Mokoena',       N'naledi.mokoena@tutorconnect.ac.za',     'Student', N'21025005', N'0621000005', N'BSCIT', 2, '2004-09-14', N'Female', N'Steve Biko Campus', NULL, 7),
 (N'Sanele',    N'Ntuli',         N'sanele.ntuli@tutorconnect.ac.za',       'Student', N'21025006', N'0621000006', N'BSCIT', 2, '2005-06-01', N'Male',   N'Steve Biko Campus', NULL, 6),
 -- BSc IT, year 3
 (N'Fatima',    N'Essop',         N'fatima.essop@tutorconnect.ac.za',       'Student', N'20033001', N'0631000001', N'BSCIT', 3, '2003-03-18', N'Female', N'ML Sultan Campus',  NULL, 11),
 (N'Kagiso',    N'Mabaso',        N'kagiso.mabaso@tutorconnect.ac.za',      'Student', N'20033002', N'0631000002', N'BSCIT', 3, '2003-10-29', N'Male',   N'Steve Biko Campus', NULL, 11),
 (N'Megan',     N'Fourie',        N'megan.fourie@tutorconnect.ac.za',       'Student', N'20033003', N'0631000003', N'BSCIT', 3, '2002-11-11', N'Female', N'Ritson Campus',     NULL, 10),
 (N'Thulani',   N'Shezi',         N'thulani.shezi@tutorconnect.ac.za',      'Student', N'20033004', N'0631000004', N'BSCIT', 3, '2003-05-06', N'Male',   N'Steve Biko Campus', NULL, 10),
 -- Diploma in ICT, years 1 and 2
 (N'Asanda',    N'Mnguni',        N'asanda.mnguni@tutorconnect.ac.za',      'Student', N'22041001', N'0641000001', N'DICT',  1, '2006-07-22', N'Female', N'Steve Biko Campus', NULL, 3),
 (N'Brandon',   N'Chetty',        N'brandon.chetty@tutorconnect.ac.za',     'Student', N'22041002', N'0641000002', N'DICT',  1, '2005-09-15', N'Male',   N'ML Sultan Campus',  NULL, 3),
 (N'Nomfundo',  N'Majola',        N'nomfundo.majola@tutorconnect.ac.za',    'Student', N'22041003', N'0641000003', N'DICT',  1, '2006-04-04', N'Female', N'Steve Biko Campus', NULL, 2),
 (N'Siyabonga', N'Mhlongo',       N'siyabonga.mhlongo@tutorconnect.ac.za',  'Student', N'22041004', N'0641000004', N'DICT',  1, '2005-12-30', N'Male',   N'Steve Biko Campus', NULL, 1),
 (N'Palesa',    N'Nkosi',         N'palesa.nkosi@tutorconnect.ac.za',       'Student', N'21044001', N'0651000001', N'DICT',  2, '2005-01-19', N'Female', N'Ritson Campus',     NULL, 8),
 (N'Tristan',   N'Adams',         N'tristan.adams@tutorconnect.ac.za',      'Student', N'21044002', N'0651000002', N'DICT',  2, '2004-08-28', N'Male',   N'ML Sultan Campus',  NULL, 8);

BEGIN TRAN;

-- 1. People (skip anyone who already exists with the same email and role)
INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, StudentNumber, Phone, CourseId, YearOfStudy,
                  DateOfBirth, Gender, Campus, PersonalEmail, PopiaConsentAt, CreatedAt)
SELECT p.FirstName, p.LastName, p.Email, @Hash, p.Role, p.StudentNumber, p.Phone, c.CourseId, p.YearOfStudy,
       p.DateOfBirth, p.Gender, p.Campus, p.PersonalEmail,
       DATEADD(MONTH, -p.JoinedMonthsAgo, dbo.fn_SaNow()), DATEADD(MONTH, -p.JoinedMonthsAgo, dbo.fn_SaNow())
FROM @People p
LEFT JOIN dbo.Courses c ON c.Code = p.CourseCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Email = p.Email AND u.Role = p.Role);

-- 2. Lecturers take the modules that had no lecturer
UPDATE m SET LecturerId = u.UserId
FROM dbo.Modules m JOIN dbo.Users u ON u.Role = 'Lecturer' AND u.Email = CASE m.Code
        WHEN N'ITSE201' THEN N'sibusiso.mthembu@tutorconnect.ac.za'
        WHEN N'ITPJ301' THEN N'priya.govender@tutorconnect.ac.za'
        WHEN N'ITNS301' THEN N'johan.vandermerwe@tutorconnect.ac.za' END
WHERE m.LecturerId IS NULL;

-- 3. Students are registered for their course's modules at their year of study
INSERT dbo.StudentModules (StudentId, ModuleId)
SELECT u.UserId, cm.ModuleId
FROM dbo.Users u
JOIN @People p ON p.Email = u.Email AND p.Role = u.Role
JOIN dbo.CourseModules cm ON cm.CourseId = u.CourseId
JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId AND m.YearLevel = u.YearOfStudy
WHERE u.Role = 'Student'
  AND NOT EXISTS (SELECT 1 FROM dbo.StudentModules sm WHERE sm.StudentId = u.UserId AND sm.ModuleId = cm.ModuleId);

-- 4. Tutors are assigned to modules (by that module's lecturer)
DECLARE @Assign TABLE (Email NVARCHAR(150), ModuleCode NVARCHAR(20));
INSERT @Assign VALUES
 (N'zinhle.mkhize@tutorconnect.ac.za',   N'ITDB101'), (N'zinhle.mkhize@tutorconnect.ac.za', N'ITWD101'),
 (N'kabelo.molefe@tutorconnect.ac.za',   N'ITPR101'),
 (N'ruan.pretorius@tutorconnect.ac.za',  N'ITPR201'), (N'ruan.pretorius@tutorconnect.ac.za', N'ITDB201'),
 (N'nadia.pillay@tutorconnect.ac.za',    N'ITSE201'),
 (N'musa.zungu@tutorconnect.ac.za',      N'ITWD101'),
 (N'lerato.mahlangu@tutorconnect.ac.za', N'ITDB201'), (N'lerato.mahlangu@tutorconnect.ac.za', N'ITNS301');

INSERT dbo.TutorModules (TutorId, ModuleId, YearOfStudy, AssignedBy)
SELECT u.UserId, m.ModuleId, m.YearLevel, m.LecturerId
FROM @Assign a
JOIN dbo.Users u ON u.Email = a.Email AND u.Role = 'Tutor'
JOIN dbo.Modules m ON m.Code = a.ModuleCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.TutorModules tm WHERE tm.TutorId = u.UserId AND tm.ModuleId = m.ModuleId);

-- lecturers asked for enough tutors to cover everyone now assigned
UPDATE m SET TutorsNeeded = x.Assigned
FROM dbo.Modules m
JOIN (SELECT ModuleId, COUNT(*) AS Assigned FROM dbo.TutorModules GROUP BY ModuleId) x ON x.ModuleId = m.ModuleId
WHERE m.TutorsNeeded < x.Assigned;

-- 5. Open booking slots on the next five weekdays for each new tutor (different times per tutor)
DECLARE @Slots TABLE (Email NVARCHAR(150), StartHour INT, Hours INT);
INSERT @Slots VALUES
 (N'zinhle.mkhize@tutorconnect.ac.za',   8, 2), (N'zinhle.mkhize@tutorconnect.ac.za',   13, 3),
 (N'kabelo.molefe@tutorconnect.ac.za',  10, 2), (N'kabelo.molefe@tutorconnect.ac.za',   15, 2),
 (N'ruan.pretorius@tutorconnect.ac.za',  9, 3),
 (N'nadia.pillay@tutorconnect.ac.za',   11, 2), (N'nadia.pillay@tutorconnect.ac.za',    16, 2),
 (N'musa.zungu@tutorconnect.ac.za',      8, 3),
 (N'lerato.mahlangu@tutorconnect.ac.za', 12, 2), (N'lerato.mahlangu@tutorconnect.ac.za', 17, 2);

DECLARE @Today DATE = CAST(dbo.fn_SaNow() AS DATE), @d INT = 1, @added INT = 0;
WHILE @added < 5 AND @d <= 10
BEGIN
    DECLARE @Day DATETIME2 = DATEADD(DAY, @d, CAST(@Today AS DATETIME2));
    -- 0 = Monday ... 6 = Sunday, whatever the server's language or DATEFIRST setting
    IF (DATEPART(WEEKDAY, @Day) + @@DATEFIRST - 2) % 7 < 5
    BEGIN
        INSERT dbo.AvailabilitySlots (TutorId, StartTime, EndTime)
        SELECT u.UserId, DATEADD(HOUR, s.StartHour, @Day), DATEADD(HOUR, s.StartHour + s.Hours, @Day)
        FROM @Slots s JOIN dbo.Users u ON u.Email = s.Email AND u.Role = 'Tutor'
        WHERE NOT EXISTS (SELECT 1 FROM dbo.AvailabilitySlots x WHERE x.TutorId = u.UserId AND x.StartTime = DATEADD(HOUR, s.StartHour, @Day));
        SET @added += 1;
    END
    SET @d += 1;
END

-- 6. A welcome notification for each new person
INSERT dbo.ActivityLog (UserId, Message)
SELECT u.UserId, N'Welcome to TutorConnect, ' + u.FirstName + N'.'
FROM dbo.Users u JOIN @People p ON p.Email = u.Email AND p.Role = u.Role
WHERE NOT EXISTS (SELECT 1 FROM dbo.ActivityLog a WHERE a.UserId = u.UserId);


-- 7. Banking details for every demo tutor, so students can pay them straight away. The account number itself is
--    encrypted with the website's key, which SQL can't do: it's left empty here and the app fills it in the first
--    time it starts (DatabaseSetup.EnsureDemoBanking). Bank, holder, branch and last four digits are complete now.
DECLARE @Bank TABLE (Email NVARCHAR(150), BankName NVARCHAR(80), Holder NVARCHAR(120), AccountType NVARCHAR(30), Branch NVARCHAR(10), Last4 NVARCHAR(4));
INSERT @Bank VALUES
 (N'tutor@tutorconnect.ac.za',           N'First National Bank (FNB)', N'S Ndlovu',    N'Cheque / current', N'250655', N'9201'),
 (N'zinhle.mkhize@tutorconnect.ac.za',   N'Capitec Bank',              N'Z Mkhize',    N'Savings',          N'470010', N'8495'),
 (N'kabelo.molefe@tutorconnect.ac.za',   N'Standard Bank',             N'K Molefe',    N'Cheque / current', N'051001', N'6274'),
 (N'ruan.pretorius@tutorconnect.ac.za',  N'Absa',                      N'R Pretorius', N'Cheque / current', N'632005', N'7365'),
 (N'nadia.pillay@tutorconnect.ac.za',    N'Nedbank',                   N'N Pillay',    N'Savings',          N'198765', N'7365'),
 (N'musa.zungu@tutorconnect.ac.za',      N'TymeBank',                  N'M Zungu',     N'Savings',          N'678910', N'4659'),
 (N'lerato.mahlangu@tutorconnect.ac.za', N'Capitec Bank',              N'L Mahlangu',  N'Savings',          N'470010', N'0476');
INSERT dbo.BankingDetails (TutorId, BankName, AccountHolder, AccountType, BranchCode, AccountNumberProtected, AccountLast4)
SELECT u.UserId, b.BankName, b.Holder, b.AccountType, b.Branch, N'', b.Last4
FROM @Bank b JOIN dbo.Users u ON u.Email = b.Email AND u.Role = 'Tutor'
WHERE NOT EXISTS (SELECT 1 FROM dbo.BankingDetails x WHERE x.TutorId = u.UserId);

COMMIT;
GO
PRINT 'TutorConnect seed data loaded.';
