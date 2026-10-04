/* =====================================================================
   TutorConnect - 05_MoreDemoUsers.sql
   Adds more demo people to an existing database WITHOUT deleting anything:
     - 1 administrator, 3 lecturers (for the modules that had none)
     - 6 tutors, assigned to modules, with open booking slots this week
     - 24 students across BSc IT and Diploma in ICT, years 1 to 3,
       registered for their year's modules
   Safe to run more than once: anyone who already exists (same email and role) is skipped.
   Every account's password is Password@123.
   Demo tutors also get banking details. Their account numbers are encrypted by the app the first
   time it starts after this script runs, so students can pay them straight away.
   ===================================================================== */
USE TutorConnect;
GO
SET NOCOUNT ON;

DECLARE @Hash NVARCHAR(100) = N'$2b$11$L7kVZX1ksoohWw3MfVBEhO5qearKipQ7qnn0ADTN5QMCymnSFI6ha';   -- Password@123
DECLARE @BSCIT INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'BSCIT');
DECLARE @DICT  INT = (SELECT CourseId FROM dbo.Courses WHERE Code = N'DICT');
IF @BSCIT IS NULL OR @DICT IS NULL
    THROW 50900, N'Run 03_SeedData.sql first: the BSCIT and DICT courses are needed.', 1;

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

SELECT Role, COUNT(*) AS People FROM dbo.Users GROUP BY Role ORDER BY Role;
PRINT 'Demo users added. Every new account''s password is Password@123.';
GO
