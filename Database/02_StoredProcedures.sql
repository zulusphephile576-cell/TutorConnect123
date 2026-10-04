/* =====================================================================
   TutorConnect - 02_StoredProcedures.sql
   Every database call the application makes goes through a procedure here.
   User-facing validation errors are raised with THROW 5xxxx so the app can
   show the message directly.
   All times are South African Standard Time (SAST, UTC+2) via dbo.fn_SaNow(),
   so they are right wherever the database server is hosted.
   ===================================================================== */
USE TutorConnect;
GO

/* =========================== ACCOUNTS =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_User_GetByEmailRole @Email NVARCHAR(150), @Role NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.*, c.Name AS CourseName
    FROM dbo.Users u LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    WHERE u.Email = @Email AND u.Role = @Role;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_GetById @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.*, c.Name AS CourseName
    FROM dbo.Users u LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    WHERE u.UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Course_List AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseId, c.Code, c.Name, c.Description, c.DepartmentId, d.Name AS DepartmentName, d.FacultyId, f.Name AS FacultyName
    FROM dbo.Courses c
    LEFT JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
    LEFT JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    ORDER BY f.Name, d.Name, c.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_Register
    @FirstName NVARCHAR(60), @LastName NVARCHAR(60), @Email NVARCHAR(150), @PasswordHash NVARCHAR(100),
    @StudentNumber NVARCHAR(20), @Phone NVARCHAR(20), @CourseId INT, @YearOfStudy INT,
    @DateOfBirth DATE = NULL, @Gender NVARCHAR(30) = NULL, @Campus NVARCHAR(80) = NULL,
    @EmergencyContactName NVARCHAR(120) = NULL, @EmergencyContactPhone NVARCHAR(20) = NULL,
    @PopiaConsent BIT = 0
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND Role = 'Student')
        THROW 50001, N'A student account with this email already exists. Sign in instead, or reset your password.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE StudentNumber = @StudentNumber AND Role = 'Student')
        THROW 50003, N'That student number is already registered. Sign in instead, or reset your password.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE CourseId = @CourseId)
        THROW 50002, N'Choose a course from the list.', 1;
    IF ISNULL(@PopiaConsent, 0) = 0
        THROW 50004, N'You need to accept the privacy notice to create an account.', 1;
    BEGIN TRAN;
    INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, StudentNumber, Phone, CourseId, YearOfStudy,
                      DateOfBirth, Gender, Campus, EmergencyContactName, EmergencyContactPhone, PopiaConsentAt)
    VALUES (@FirstName, @LastName, @Email, @PasswordHash, 'Student', @StudentNumber, @Phone, @CourseId, @YearOfStudy,
            @DateOfBirth, @Gender, @Campus, @EmergencyContactName, @EmergencyContactPhone, dbo.fn_SaNow());
    DECLARE @Id INT = SCOPE_IDENTITY();
    -- Every module linked to the course (at the student's year level) is registered automatically.
    INSERT dbo.StudentModules (StudentId, ModuleId)
    SELECT @Id, cm.ModuleId
    FROM dbo.CourseModules cm JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId
    WHERE cm.CourseId = @CourseId AND m.YearLevel = @YearOfStudy;
    INSERT dbo.ActivityLog (UserId, Message)
    VALUES (@Id, N'Welcome to TutorConnect. Your modules were registered automatically.');
    COMMIT;
    SELECT @Id AS UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_VerifyIdentity
    @Email NVARCHAR(150), @Role NVARCHAR(20), @LastName NVARCHAR(60), @StudentNumber NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId FROM dbo.Users
    WHERE Email = @Email AND Role = @Role AND IsActive = 1
      AND LastName = @LastName
      AND (@Role NOT IN ('Student','Tutor') OR StudentNumber = @StudentNumber);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_PasswordReset_Create @UserId INT, @Token NVARCHAR(64) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PasswordResetTokens SET Used = 1 WHERE UserId = @UserId AND Used = 0;
    INSERT dbo.PasswordResetTokens (Token, UserId, ExpiresAt) VALUES (@Token, @UserId, DATEADD(MINUTE, 15, dbo.fn_SaNow()));
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_PasswordReset_Check @Token NVARCHAR(64) AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM dbo.PasswordResetTokens WHERE Token = @Token AND Used = 0 AND ExpiresAt > dbo.fn_SaNow();
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_PasswordReset_Consume @Token NVARCHAR(64), @PasswordHash NVARCHAR(100) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @UserId INT;
    SELECT @UserId = UserId FROM dbo.PasswordResetTokens WHERE Token = @Token AND Used = 0 AND ExpiresAt > dbo.fn_SaNow();
    IF @UserId IS NULL BEGIN SELECT 0 AS Ok; RETURN; END
    BEGIN TRAN;
    UPDATE dbo.Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId;
    UPDATE dbo.PasswordResetTokens SET Used = 1 WHERE Token = @Token;
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@UserId, N'Your password was reset.');
    COMMIT;
    SELECT 1 AS Ok;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_ChangePassword @UserId INT, @PasswordHash NVARCHAR(100) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId;
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@UserId, N'You changed your password.');
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_UpdateSettings @UserId INT, @DarkMode BIT, @EmailNotifications BIT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET DarkMode = @DarkMode, EmailNotifications = @EmailNotifications WHERE UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Activity_Add @UserId INT, @Message NVARCHAR(400) AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@UserId, @Message);
END
GO

/* Reminders: anything starting within the next 20 minutes */
CREATE OR ALTER PROCEDURE dbo.sp_Reminders_Upcoming @UserId INT, @Role NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2 = dbo.fn_SaNow(), @Soon DATETIME2 = DATEADD(MINUTE, 20, dbo.fn_SaNow());
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @UserId);

    IF @Role = 'Student'
    BEGIN
        SELECT 'session-' + CAST(b.BookingId AS NVARCHAR(10)) AS [Key],
               N'Session for ' + m.Code + N' with ' + t.FirstName + N' ' + t.LastName AS Title, b.StartTime
        FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users t ON t.UserId = b.TutorId
        WHERE b.StudentId = @UserId AND b.Status IN ('Pending','Confirmed') AND b.StartTime BETWEEN @Now AND @Soon
        UNION ALL
        SELECT 'tutorial-' + CAST(tu.TutorialId AS NVARCHAR(10)),
               N'Tutorial: ' + tu.Title + N' (' + m.Code + N')' + ISNULL(N' in ' + tu.Venue, N''), tu.StartTime
        FROM dbo.Tutorials tu
        JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId AND sm.StudentId = @UserId
        JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId
        WHERE tu.YearOfStudy = @Year AND tu.StartTime BETWEEN @Now AND @Soon;
    END
    ELSE IF @Role = 'Tutor'
    BEGIN
        SELECT 'session-' + CAST(b.BookingId AS NVARCHAR(10)) AS [Key],
               N'Session for ' + m.Code + N' with ' + s.FirstName + N' ' + s.LastName AS Title, b.StartTime
        FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
        WHERE b.TutorId = @UserId AND b.Status = 'Confirmed' AND b.StartTime BETWEEN @Now AND @Soon
        UNION ALL
        SELECT 'tutorial-' + CAST(tu.TutorialId AS NVARCHAR(10)),
               N'You are running: ' + tu.Title + N' (' + m.Code + N')', tu.StartTime
        FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId
        WHERE tu.TutorId = @UserId AND tu.StartTime BETWEEN @Now AND @Soon;
    END
    ELSE
        SELECT CAST(NULL AS NVARCHAR(30)) AS [Key], CAST(NULL AS NVARCHAR(200)) AS Title, CAST(NULL AS DATETIME2) AS StartTime WHERE 1 = 0;
END
GO

/* =========================== STUDENT =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_Student_Dashboard @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @StudentId);

    -- 0: upcoming sessions
    SELECT TOP 5 b.BookingId, b.StartTime, b.EndTime, b.Hours, b.Status, b.Amount,
           m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.StudentId = @StudentId AND b.StartTime >= dbo.fn_SaNow() AND b.Status IN ('Pending','Confirmed')
    ORDER BY b.StartTime;

    -- 1: upcoming tutorials
    SELECT TOP 5 tu.TutorialId, tu.Title, tu.Venue, tu.StartTime, tu.EndTime, tu.YearOfStudy,
           m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.Tutorials tu
    JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users t ON t.UserId = tu.TutorId
    WHERE tu.YearOfStudy = @Year AND tu.EndTime >= dbo.fn_SaNow()
    ORDER BY tu.StartTime;

    -- 2: activity
    SELECT TOP 8 Message, CreatedAt FROM dbo.ActivityLog WHERE UserId = @StudentId ORDER BY CreatedAt DESC;

    -- 3: announcements
    SELECT TOP 5 a.AnnouncementId, a.Title, a.Message, a.CreatedAt, a.YearOfStudy, m.Code AS ModuleCode,
           t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.Announcements a
    JOIN dbo.StudentModules sm ON sm.ModuleId = a.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Modules m ON m.ModuleId = a.ModuleId JOIN dbo.Users t ON t.UserId = a.TutorId
    WHERE a.YearOfStudy = @Year
    ORDER BY a.CreatedAt DESC;

    -- 4: assigned tutors
    SELECT tm.TutorId, t.FirstName + ' ' + t.LastName AS TutorName, t.Email AS TutorEmail,
           m.ModuleId, m.Code AS ModuleCode, m.Name AS ModuleName, tm.YearOfStudy
    FROM dbo.TutorModules tm
    JOIN dbo.StudentModules sm ON sm.ModuleId = tm.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Users t ON t.UserId = tm.TutorId JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    WHERE tm.YearOfStudy = @Year AND t.IsActive = 1
    ORDER BY m.Code;

    -- 5: newly opened slots from the student's tutors
    SELECT TOP 6 s.SlotId, s.TutorId, s.StartTime, s.EndTime, s.CreatedAt, t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.AvailabilitySlots s JOIN dbo.Users t ON t.UserId = s.TutorId
    WHERE s.IsBooked = 0 AND s.StartTime > dbo.fn_SaNow() AND s.CreatedAt >= DATEADD(DAY, -7, dbo.fn_SaNow())
      AND s.TutorId IN (SELECT tm.TutorId FROM dbo.TutorModules tm
                        JOIN dbo.StudentModules sm ON sm.ModuleId = tm.ModuleId AND sm.StudentId = @StudentId
                        WHERE tm.YearOfStudy = @Year)
    ORDER BY s.CreatedAt DESC;

    -- 6: stats
    DECLARE @Att TABLE (Status NVARCHAR(10));
    INSERT @Att SELECT AttendanceStatus FROM dbo.Bookings WHERE StudentId = @StudentId AND AttendanceStatus IS NOT NULL;
    INSERT @Att SELECT Status FROM dbo.TutorialAttendance WHERE StudentId = @StudentId;
    INSERT @Att SELECT Status FROM dbo.ClassRegisterAttendance WHERE StudentId = @StudentId;

    SELECT
        (SELECT COUNT(*) FROM dbo.StudentModules WHERE StudentId = @StudentId) AS ModuleCount,
        (SELECT COUNT(*) FROM dbo.Bookings WHERE StudentId = @StudentId AND Status IN ('Pending','Confirmed') AND StartTime >= dbo.fn_SaNow()) AS UpcomingSessions,
        (SELECT COUNT(*) FROM @Att) AS AttendanceRecords,
        CAST(CASE WHEN (SELECT COUNT(*) FROM @Att) = 0 THEN 0
             ELSE 100.0 * (SELECT COUNT(*) FROM @Att WHERE Status IN ('Present','Late')) / (SELECT COUNT(*) FROM @Att) END AS DECIMAL(5,1)) AS AttendanceRate;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_SearchTutors @StudentId INT, @Term NVARCHAR(100) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @StudentId);
    SET @Term = NULLIF(LTRIM(RTRIM(@Term)), N'');

    SELECT tm.TutorId, t.FirstName + ' ' + t.LastName AS TutorName, t.Email AS TutorEmail,
           m.ModuleId, m.Code AS ModuleCode, m.Name AS ModuleName, tm.YearOfStudy,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s
            WHERE s.TutorId = tm.TutorId AND s.IsBooked = 0 AND s.StartTime > dbo.fn_SaNow()) AS OpenSlots
    FROM dbo.TutorModules tm
    JOIN dbo.StudentModules sm ON sm.ModuleId = tm.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    JOIN dbo.Users t ON t.UserId = tm.TutorId
    WHERE tm.YearOfStudy = @Year AND t.IsActive = 1
      AND (@Term IS NULL
           OR m.Code LIKE N'%' + @Term + N'%'
           OR m.Name LIKE N'%' + @Term + N'%'
           OR t.FirstName + N' ' + t.LastName LIKE N'%' + @Term + N'%')
    ORDER BY m.Code, TutorName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_OpenSlots @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT SlotId, TutorId, StartTime, EndTime, IsBooked, CreatedAt
    FROM dbo.AvailabilitySlots
    WHERE TutorId = @TutorId AND IsBooked = 0 AND StartTime > dbo.fn_SaNow()
    ORDER BY StartTime;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_Create
    @PaymentRef NVARCHAR(40), @StudentId INT, @TutorId INT, @ModuleId INT, @SlotId INT, @Hours INT, @HourlyRate DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @StudentId);
    DECLARE @Start DATETIME2, @End DATETIME2;

    IF NOT EXISTS (SELECT 1 FROM dbo.TutorModules tm JOIN dbo.StudentModules sm ON sm.ModuleId = tm.ModuleId AND sm.StudentId = @StudentId
                   WHERE tm.TutorId = @TutorId AND tm.ModuleId = @ModuleId AND tm.YearOfStudy = @Year)
        THROW 50101, N'This tutor does not cover that module for your year of study.', 1;

    SELECT @Start = StartTime, @End = EndTime FROM dbo.AvailabilitySlots
    WHERE SlotId = @SlotId AND TutorId = @TutorId AND IsBooked = 0 AND StartTime > dbo.fn_SaNow();
    IF @Start IS NULL THROW 50102, N'That time slot is no longer available. Pick another one.', 1;
    IF @Hours < 1 OR @Hours > 8 THROW 50103, N'Choose between 1 and 8 hours.', 1;
    IF DATEADD(HOUR, @Hours, @Start) > @End THROW 50104, N'That slot is too short for the number of hours you chose.', 1;

    INSERT dbo.Payments (PaymentRef, StudentId, TutorId, ModuleId, SlotId, SessionStart, Hours, HourlyRate, Amount)
    VALUES (@PaymentRef, @StudentId, @TutorId, @ModuleId, @SlotId, @Start, @Hours, @HourlyRate, @Hours * @HourlyRate);
    SELECT SCOPE_IDENTITY() AS PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_GetByRef @PaymentRef NVARCHAR(40) AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.*, s.FirstName + ' ' + s.LastName AS StudentName, s.FirstName AS StudentFirstName, s.LastName AS StudentLastName,
           s.Email AS StudentEmail, t.FirstName + ' ' + t.LastName AS TutorName, t.Email AS TutorEmail,
           t.EmailNotifications AS TutorWantsEmail, m.Code AS ModuleCode, m.Name AS ModuleName
    FROM dbo.Payments p
    JOIN dbo.Users s ON s.UserId = p.StudentId JOIN dbo.Users t ON t.UserId = p.TutorId
    JOIN dbo.Modules m ON m.ModuleId = p.ModuleId
    WHERE p.PaymentRef = @PaymentRef;
END
GO

/* Called by the PayFast ITN handler (idempotent: only the first successful call creates the booking) */
CREATE OR ALTER PROCEDURE dbo.sp_Payment_Complete @PaymentRef NVARCHAR(40), @PfPaymentId NVARCHAR(50) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PaymentId INT, @Status NVARCHAR(20), @BookingId INT, @WasNew BIT = 0,
            @SlotId INT, @Hours INT, @StudentId INT, @TutorId INT, @Start DATETIME2, @Code NVARCHAR(20);

    BEGIN TRAN;
    SELECT @PaymentId = p.PaymentId, @Status = p.Status, @BookingId = p.BookingId, @SlotId = p.SlotId, @Hours = p.Hours,
           @StudentId = p.StudentId, @TutorId = p.TutorId, @Start = p.SessionStart, @Code = m.Code
    FROM dbo.Payments p WITH (UPDLOCK, HOLDLOCK) JOIN dbo.Modules m ON m.ModuleId = p.ModuleId
    WHERE p.PaymentRef = @PaymentRef;

    IF @PaymentId IS NULL THROW 50110, N'Payment reference not found.', 1;

    IF @Status IN ('Pending','Cancelled')
       AND EXISTS (SELECT 1 FROM dbo.AvailabilitySlots WITH (UPDLOCK, HOLDLOCK) WHERE SlotId = @SlotId AND IsBooked = 1)
        -- someone else paid for this slot first: refuse, so nobody is double-booked (the transaction rolls back)
        THROW 50112, N'Sorry, someone else booked this slot moments ago. You haven''t been charged; please choose another time.', 1;
    IF @Status IN ('Pending','Cancelled')
    BEGIN
        INSERT dbo.Bookings (StudentId, TutorId, ModuleId, SlotId, PaymentId, StartTime, EndTime, Hours, Amount, Status)
        SELECT StudentId, TutorId, ModuleId, SlotId, PaymentId, SessionStart, DATEADD(HOUR, Hours, SessionStart), Hours, Amount, 'Pending'
        FROM dbo.Payments WHERE PaymentId = @PaymentId;
        SET @BookingId = SCOPE_IDENTITY();

        IF @SlotId IS NOT NULL
        BEGIN
            -- Any time left over in the slot becomes a new open slot (if at least an hour).
            INSERT dbo.AvailabilitySlots (TutorId, StartTime, EndTime)
            SELECT TutorId, DATEADD(HOUR, @Hours, StartTime), EndTime
            FROM dbo.AvailabilitySlots
            WHERE SlotId = @SlotId AND DATEDIFF(MINUTE, DATEADD(HOUR, @Hours, StartTime), EndTime) >= 60;

            UPDATE dbo.AvailabilitySlots SET IsBooked = 1, EndTime = DATEADD(HOUR, @Hours, StartTime) WHERE SlotId = @SlotId;
        END

        UPDATE dbo.Payments SET Status = 'Complete', PfPaymentId = @PfPaymentId, CompletedAt = dbo.fn_SaNow(), BookingId = @BookingId
        WHERE PaymentId = @PaymentId;

        INSERT dbo.ActivityLog (UserId, Message) VALUES
            (@StudentId, N'Payment sent. Your ' + @Code + N' session request on ' + FORMAT(@Start, 'dd MMM HH:mm') + N' was sent to your tutor.'),
            (@TutorId,   N'New paid booking request for ' + @Code + N' on ' + FORMAT(@Start, 'dd MMM HH:mm') + N'.');
        SET @WasNew = 1;
    END
    COMMIT;
    SELECT @PaymentId AS PaymentId, @BookingId AS BookingId, @WasNew AS WasNew;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_SetStatus @PaymentRef NVARCHAR(40), @Status NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Payments SET Status = @Status WHERE PaymentRef = @PaymentRef AND Status = 'Pending';
    SELECT @@ROWCOUNT AS Affected;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_Bookings @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.*, m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName, t.Email AS TutorEmail
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.StudentId = @StudentId
    ORDER BY CASE WHEN b.StartTime >= dbo.fn_SaNow() THEN 0 ELSE 1 END, b.StartTime;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_CancelByStudent @BookingId INT, @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Bookings SET Status = 'Cancelled', UpdatedAt = dbo.fn_SaNow()
    WHERE BookingId = @BookingId AND StudentId = @StudentId AND Status IN ('Pending','Confirmed') AND StartTime > dbo.fn_SaNow();
    DECLARE @n INT = @@ROWCOUNT;
    IF @n = 1
    BEGIN
        UPDATE dbo.AvailabilitySlots SET IsBooked = 0 WHERE SlotId = (SELECT SlotId FROM dbo.Bookings WHERE BookingId = @BookingId);
        INSERT dbo.ActivityLog (UserId, Message)
        SELECT b.TutorId, s.FirstName + N' ' + s.LastName + N' cancelled their ' + m.Code + N' session on ' + FORMAT(b.StartTime, 'dd MMM HH:mm') + N'.'
        FROM dbo.Bookings b JOIN dbo.Users s ON s.UserId = b.StudentId JOIN dbo.Modules m ON m.ModuleId = b.ModuleId
        WHERE b.BookingId = @BookingId;
    END
    SELECT @n AS Affected;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_Tutorials @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @StudentId);
    SELECT tu.TutorialId, tu.TutorId, tu.ModuleId, tu.YearOfStudy, tu.Title, tu.Description, tu.Venue, tu.StartTime, tu.EndTime,
           m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName, a.Status AS MyStatus
    FROM dbo.Tutorials tu
    JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users t ON t.UserId = tu.TutorId
    LEFT JOIN dbo.TutorialAttendance a ON a.TutorialId = tu.TutorialId AND a.StudentId = @StudentId
    WHERE tu.YearOfStudy = @Year
    ORDER BY CASE WHEN tu.EndTime >= dbo.fn_SaNow() THEN 0 ELSE 1 END, tu.StartTime;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_Get @TutorialId INT, @StudentId INT = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT tu.*, m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
            WHERE sm.ModuleId = tu.ModuleId AND u.YearOfStudy = tu.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1) AS EnrolledCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Present') AS PresentCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Late') AS LateCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Absent') AS AbsentCount,
           (SELECT a.Status FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.StudentId = @StudentId) AS MyStatus,
           CAST(CASE WHEN @StudentId IS NULL THEN 1 ELSE
                (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
                 WHERE sm.StudentId = @StudentId AND sm.ModuleId = tu.ModuleId AND u.YearOfStudy = tu.YearOfStudy) END AS BIT) AS IsEnrolled
    FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users t ON t.UserId = tu.TutorId
    WHERE tu.TutorialId = @TutorialId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_GetByToken @Token UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT = (SELECT TutorialId FROM dbo.Tutorials WHERE QrToken = @Token);
    EXEC dbo.sp_Tutorial_Get @TutorialId = @Id;
END
GO

/* QR check-in: the student id always comes from the server-side session, never the request. */
CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_QrCheckIn @Token UNIQUEIDENTIFIER, @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TId INT, @ModuleId INT, @Year INT, @Start DATETIME2, @End DATETIME2, @Title NVARCHAR(150);
    SELECT @TId = TutorialId, @ModuleId = ModuleId, @Year = YearOfStudy, @Start = StartTime, @End = EndTime, @Title = Title
    FROM dbo.Tutorials WHERE QrToken = @Token;

    IF @TId IS NULL BEGIN SELECT 'NotFound' AS Result, CAST(NULL AS NVARCHAR(10)) AS Status, CAST(NULL AS INT) AS TutorialId; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
                   WHERE sm.StudentId = @StudentId AND sm.ModuleId = @ModuleId AND u.YearOfStudy = @Year)
    BEGIN SELECT 'NotEnrolled' AS Result, CAST(NULL AS NVARCHAR(10)) AS Status, @TId AS TutorialId; RETURN; END

    -- Check-in opens when the tutorial starts and closes when it ends.
    IF dbo.fn_SaNow() < @Start OR dbo.fn_SaNow() > @End
    BEGIN SELECT 'Closed' AS Result, CAST(NULL AS NVARCHAR(10)) AS Status, @TId AS TutorialId; RETURN; END

    IF EXISTS (SELECT 1 FROM dbo.TutorialAttendance WHERE TutorialId = @TId AND StudentId = @StudentId AND Status = 'Present')
    BEGIN
        SELECT 'Already' AS Result, Status, @TId AS TutorialId FROM dbo.TutorialAttendance WHERE TutorialId = @TId AND StudentId = @StudentId;
        RETURN;
    END

    DECLARE @Status NVARCHAR(10) = 'Present';   -- scanning the code marks the student present

    MERGE dbo.TutorialAttendance AS tgt
    USING (SELECT @TId AS TutorialId, @StudentId AS StudentId) AS src
       ON tgt.TutorialId = src.TutorialId AND tgt.StudentId = src.StudentId
    WHEN MATCHED THEN UPDATE SET Status = @Status, Method = 'QR', MarkedAt = dbo.fn_SaNow()
    WHEN NOT MATCHED THEN INSERT (TutorialId, StudentId, Status, Method) VALUES (@TId, @StudentId, @Status, 'QR');

    INSERT dbo.ActivityLog (UserId, Message) VALUES (@StudentId, N'You checked in to "' + @Title + N'" (' + @Status + N').');
    SELECT 'OK' AS Result, @Status AS Status, @TId AS TutorialId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_Attendance @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Session' AS Source, b.BookingId AS SourceId, N'One-on-one session' AS Title, m.Code AS ModuleCode, m.Name AS ModuleName,
           t.FirstName + ' ' + t.LastName AS TutorName, b.StartTime AS [Date], b.AttendanceStatus AS Status
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.StudentId = @StudentId AND b.AttendanceStatus IS NOT NULL
    UNION ALL
    SELECT 'Tutorial', tu.TutorialId, tu.Title, m.Code, m.Name, t.FirstName + ' ' + t.LastName, tu.StartTime, a.Status
    FROM dbo.TutorialAttendance a JOIN dbo.Tutorials tu ON tu.TutorialId = a.TutorialId
    JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users t ON t.UserId = tu.TutorId
    WHERE a.StudentId = @StudentId
    UNION ALL
    SELECT 'Class register', cr.RegisterId, cr.Title, m.Code, m.Name, t.FirstName + ' ' + t.LastName, CAST(cr.RegisterDate AS DATETIME2), ca.Status
    FROM dbo.ClassRegisterAttendance ca JOIN dbo.ClassRegisters cr ON cr.RegisterId = ca.RegisterId
    JOIN dbo.Modules m ON m.ModuleId = cr.ModuleId JOIN dbo.Users t ON t.UserId = cr.TutorId
    WHERE ca.StudentId = @StudentId
    ORDER BY [Date] DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Student_Modules @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = (SELECT YearOfStudy FROM dbo.Users WHERE UserId = @StudentId);
    -- A module can have several tutors; show the first by name plus how many there are.
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester, m.Description,
           l.FirstName + ' ' + l.LastName AS LecturerName,
           tt.TutorName, tt.TutorId,
           (SELECT COUNT(*) FROM dbo.TutorModules x WHERE x.ModuleId = m.ModuleId AND x.YearOfStudy = @Year) AS TutorCount
    FROM dbo.StudentModules sm JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId
    LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId
    OUTER APPLY (SELECT TOP 1 t.FirstName + ' ' + t.LastName AS TutorName, t.UserId AS TutorId
                 FROM dbo.TutorModules tm JOIN dbo.Users t ON t.UserId = tm.TutorId
                 WHERE tm.ModuleId = m.ModuleId AND tm.YearOfStudy = @Year AND t.IsActive = 1
                 ORDER BY t.FirstName, t.LastName) tt
    WHERE sm.StudentId = @StudentId
    ORDER BY m.Code;
END
GO

/* =========================== TUTOR =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Dashboard @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    -- 0: today's sessions
    SELECT b.BookingId, b.StartTime, b.EndTime, b.Hours, b.Status, m.Code AS ModuleCode, m.Name AS ModuleName,
           s.FirstName + ' ' + s.LastName AS StudentName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
    WHERE b.TutorId = @TutorId AND CAST(b.StartTime AS DATE) = CAST(dbo.fn_SaNow() AS DATE) AND b.Status IN ('Pending','Confirmed','Completed')
    ORDER BY b.StartTime;

    -- 1: upcoming (after today)
    SELECT TOP 8 b.BookingId, b.StartTime, b.EndTime, b.Hours, b.Status, m.Code AS ModuleCode, m.Name AS ModuleName,
           s.FirstName + ' ' + s.LastName AS StudentName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
    WHERE b.TutorId = @TutorId AND CAST(b.StartTime AS DATE) > CAST(dbo.fn_SaNow() AS DATE) AND b.Status IN ('Pending','Confirmed')
    ORDER BY b.StartTime;

    -- 2: pending requests
    SELECT b.BookingId, b.StartTime, b.EndTime, b.Hours, b.Status, b.Amount, m.Code AS ModuleCode, m.Name AS ModuleName,
           s.FirstName + ' ' + s.LastName AS StudentName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
    WHERE b.TutorId = @TutorId AND b.Status = 'Pending' AND b.StartTime > dbo.fn_SaNow()
    ORDER BY b.StartTime;

    -- 3: new lecturer content waiting for review
    SELECT TOP 5 c.ContentId, c.Title, c.UploadedAt, m.Code AS ModuleCode, u.FirstName + ' ' + u.LastName AS UploaderName
    FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId LEFT JOIN dbo.Users u ON u.UserId = c.UploadedBy
    WHERE c.UploaderRole = 'Lecturer' AND c.IsReviewed = 0
      AND EXISTS (SELECT 1 FROM dbo.TutorModules tm WHERE tm.TutorId = @TutorId AND tm.ModuleId = c.ModuleId
                  AND (c.YearOfStudy IS NULL OR c.YearOfStudy = tm.YearOfStudy))
    ORDER BY c.UploadedAt DESC;

    -- 4: upcoming tutorials
    SELECT TOP 5 tu.TutorialId, tu.Title, tu.Venue, tu.StartTime, tu.EndTime, tu.YearOfStudy, m.Code AS ModuleCode
    FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId
    WHERE tu.TutorId = @TutorId AND tu.EndTime >= dbo.fn_SaNow()
    ORDER BY tu.StartTime;

    -- 5: stats
    SELECT
        (SELECT COUNT(*) FROM dbo.Bookings WHERE TutorId = @TutorId AND Status = 'Pending' AND StartTime > dbo.fn_SaNow()) AS PendingRequests,
        (SELECT COUNT(*) FROM dbo.Bookings WHERE TutorId = @TutorId AND Status = 'Confirmed' AND StartTime > dbo.fn_SaNow()) AS UpcomingSessions,
        (SELECT COUNT(*) FROM dbo.AvailabilitySlots WHERE TutorId = @TutorId AND IsBooked = 0 AND StartTime > dbo.fn_SaNow()) AS OpenSlots,
        (SELECT ISNULL(SUM(Hours), 0) FROM dbo.Bookings WHERE TutorId = @TutorId AND Status = 'Completed') AS HoursTutored,
        (SELECT COUNT(*) FROM dbo.TutorModules WHERE TutorId = @TutorId) AS ModuleCount;

    -- 6: activity
    SELECT TOP 6 Message, CreatedAt FROM dbo.ActivityLog WHERE UserId = @TutorId ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Assignments @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT tm.TutorModuleId, tm.TutorId, tm.ModuleId, tm.YearOfStudy, tm.AssignedAt, m.Code AS ModuleCode, m.Name AS ModuleName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
            WHERE sm.ModuleId = tm.ModuleId AND u.YearOfStudy = tm.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1) AS StudentCount
    FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    WHERE tm.TutorId = @TutorId
    ORDER BY m.Code, tm.YearOfStudy;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Slots @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.SlotId, s.TutorId, s.StartTime, s.EndTime, s.IsBooked, s.CreatedAt, bk.StudentName
    FROM dbo.AvailabilitySlots s
    OUTER APPLY (SELECT TOP 1 u.FirstName + ' ' + u.LastName AS StudentName FROM dbo.Bookings b JOIN dbo.Users u ON u.UserId = b.StudentId
                 WHERE b.SlotId = s.SlotId AND b.Status IN ('Pending','Confirmed','Completed') ORDER BY b.CreatedAt DESC) bk
    WHERE s.TutorId = @TutorId AND s.EndTime >= CAST(CAST(dbo.fn_SaNow() AS DATE) AS DATETIME2)
    ORDER BY s.StartTime;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Slot_Add @TutorId INT, @StartTime DATETIME2, @EndTime DATETIME2 AS
BEGIN
    SET NOCOUNT ON;
    IF @StartTime <= dbo.fn_SaNow() THROW 50201, N'The slot must start in the future.', 1;
    IF DATEDIFF(MINUTE, @StartTime, @EndTime) < 60 THROW 50202, N'A slot must be at least one hour long.', 1;
    IF DATEDIFF(HOUR, @StartTime, @EndTime) > 8 THROW 50203, N'A slot can be at most eight hours long.', 1;
    IF EXISTS (SELECT 1 FROM dbo.AvailabilitySlots WHERE TutorId = @TutorId AND StartTime < @EndTime AND EndTime > @StartTime)
        THROW 50204, N'This overlaps a slot you already have.', 1;
    INSERT dbo.AvailabilitySlots (TutorId, StartTime, EndTime) VALUES (@TutorId, @StartTime, @EndTime);
    SELECT SCOPE_IDENTITY() AS SlotId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Slot_Delete @SlotId INT, @TutorId INT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF EXISTS (SELECT 1 FROM dbo.AvailabilitySlots WHERE SlotId = @SlotId AND TutorId = @TutorId AND IsBooked = 1)
        THROW 50205, N'This slot is booked. Decline or reschedule the session instead.', 1;
    BEGIN TRAN;
    UPDATE dbo.Bookings SET SlotId = NULL WHERE SlotId = @SlotId;
    UPDATE dbo.Payments SET SlotId = NULL WHERE SlotId = @SlotId;
    DELETE dbo.AvailabilitySlots WHERE SlotId = @SlotId AND TutorId = @TutorId;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Sessions @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.*, m.Code AS ModuleCode, m.Name AS ModuleName, s.FirstName + ' ' + s.LastName AS StudentName,
           s.Email AS StudentEmail, s.StudentNumber
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
    WHERE b.TutorId = @TutorId
    ORDER BY CASE b.Status WHEN 'Pending' THEN 0 WHEN 'Confirmed' THEN 1 ELSE 2 END, b.StartTime DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_Get @BookingId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.*, m.Code AS ModuleCode, m.Name AS ModuleName, s.FirstName + ' ' + s.LastName AS StudentName, s.Email AS StudentEmail,
           t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId
    JOIN dbo.Users s ON s.UserId = b.StudentId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.BookingId = @BookingId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_SetStatus @BookingId INT, @TutorId INT, @Status NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Bookings SET Status = @Status, UpdatedAt = dbo.fn_SaNow()
    WHERE BookingId = @BookingId AND TutorId = @TutorId
      AND ((@Status IN ('Confirmed','Declined') AND Status = 'Pending')
        OR (@Status = 'Cancelled' AND Status IN ('Pending','Confirmed')));
    DECLARE @n INT = @@ROWCOUNT;
    IF @n = 1
    BEGIN
        IF @Status IN ('Declined','Cancelled')
            UPDATE dbo.AvailabilitySlots SET IsBooked = 0 WHERE SlotId = (SELECT SlotId FROM dbo.Bookings WHERE BookingId = @BookingId);
        INSERT dbo.ActivityLog (UserId, Message)
        SELECT b.StudentId, N'Your ' + m.Code + N' session on ' + FORMAT(b.StartTime, 'dd MMM HH:mm') + N' was ' + LOWER(@Status) + N' by your tutor.'
        FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId WHERE b.BookingId = @BookingId;
    END
    SELECT @n AS Affected;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_Reschedule @BookingId INT, @TutorId INT, @NewStart DATETIME2 AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @NewStart <= dbo.fn_SaNow() THROW 50210, N'Choose a date and time in the future.', 1;
    DECLARE @Hours INT, @OldSlot INT;
    SELECT @Hours = Hours, @OldSlot = SlotId FROM dbo.Bookings
    WHERE BookingId = @BookingId AND TutorId = @TutorId AND Status IN ('Pending','Confirmed');
    IF @Hours IS NULL THROW 50211, N'Only pending or confirmed sessions can be rescheduled.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Bookings WHERE TutorId = @TutorId AND BookingId <> @BookingId AND Status IN ('Pending','Confirmed')
               AND StartTime < DATEADD(HOUR, @Hours, @NewStart) AND EndTime > @NewStart)
        THROW 50212, N'You already have a session at that time.', 1;

    BEGIN TRAN;
    UPDATE dbo.Bookings SET StartTime = @NewStart, EndTime = DATEADD(HOUR, @Hours, @NewStart), SlotId = NULL, UpdatedAt = dbo.fn_SaNow()
    WHERE BookingId = @BookingId;
    IF @OldSlot IS NOT NULL UPDATE dbo.AvailabilitySlots SET IsBooked = 0 WHERE SlotId = @OldSlot;
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT b.StudentId, N'Your ' + m.Code + N' session was moved to ' + FORMAT(@NewStart, 'ddd dd MMM HH:mm') + N'.'
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId WHERE b.BookingId = @BookingId;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Booking_MarkAttendance @BookingId INT, @TutorId INT, @Status NVARCHAR(10) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Bookings SET AttendanceStatus = @Status, Status = 'Completed', UpdatedAt = dbo.fn_SaNow()
    WHERE BookingId = @BookingId AND TutorId = @TutorId AND Status IN ('Confirmed','Completed') AND StartTime <= dbo.fn_SaNow();
    SELECT @@ROWCOUNT AS Affected;
END
GO

/* ---------- Tutorials ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_Create
    @TutorId INT, @ModuleId INT, @YearOfStudy INT, @Title NVARCHAR(150), @Description NVARCHAR(1000),
    @Venue NVARCHAR(150), @StartTime DATETIME2, @EndTime DATETIME2
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @TutorId AND ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy)
        THROW 50220, N'You are not assigned to that module for that year of study.', 1;
    IF @EndTime <= @StartTime THROW 50221, N'The end time must be after the start time.', 1;

    BEGIN TRAN;
    -- Every tutorial gets its own QR token (NEWID) and its own short check-in code.
    DECLARE @alphabet NCHAR(31) = N'ABCDEFGHJKLMNPQRSTUVWXYZ2345679', @code NVARCHAR(8), @i INT;
    WHILE 1 = 1
    BEGIN
        SET @code = N''; SET @i = 0;
        WHILE @i < 6 BEGIN SET @code += SUBSTRING(@alphabet, 1 + ABS(CHECKSUM(NEWID())) % 31, 1); SET @i += 1; END
        IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials WHERE CheckInCode = @code) BREAK;
    END
    INSERT dbo.Tutorials (TutorId, ModuleId, YearOfStudy, Title, Description, Venue, StartTime, EndTime, QrToken, CheckInCode)
    VALUES (@TutorId, @ModuleId, @YearOfStudy, @Title, @Description, @Venue, @StartTime, @EndTime, NEWID(), @code);
    DECLARE @Id INT = SCOPE_IDENTITY();

    INSERT dbo.ActivityLog (UserId, Message)
    SELECT u.UserId, N'New tutorial: ' + @Title + N' (' + m.Code + N') on ' + FORMAT(@StartTime, 'ddd dd MMM HH:mm') + N'.'
    FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId
    WHERE sm.ModuleId = @ModuleId AND u.YearOfStudy = @YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1;
    COMMIT;
    SELECT @Id AS TutorialId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Tutorials @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT tu.*, m.Code AS ModuleCode, m.Name AS ModuleName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
            WHERE sm.ModuleId = tu.ModuleId AND u.YearOfStudy = tu.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1) AS EnrolledCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Present') AS PresentCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Late') AS LateCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Absent') AS AbsentCount
    FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId
    WHERE tu.TutorId = @TutorId
    ORDER BY CASE WHEN tu.EndTime >= dbo.fn_SaNow() THEN 0 ELSE 1 END, tu.StartTime DESC;
END
GO

-- The class register is generated from every student enrolled in the module for that year.
CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_Register @TutorialId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId AS StudentId, u.FirstName + ' ' + u.LastName AS StudentName, u.StudentNumber, u.Email,
           a.Status, a.Method, a.MarkedAt
    FROM dbo.Tutorials tu
    JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId
    JOIN dbo.Users u ON u.UserId = sm.StudentId AND u.YearOfStudy = tu.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1
    LEFT JOIN dbo.TutorialAttendance a ON a.TutorialId = tu.TutorialId AND a.StudentId = u.UserId
    WHERE tu.TutorialId = @TutorialId
    ORDER BY u.LastName, u.FirstName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_MarkAttendance @TutorialId INT, @StudentId INT, @Status NVARCHAR(10), @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials WHERE TutorialId = @TutorialId AND TutorId = @TutorId)
        THROW 50230, N'You can only mark registers for your own tutorials.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials tu JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId AND sm.StudentId = @StudentId
                   JOIN dbo.Users u ON u.UserId = sm.StudentId AND u.YearOfStudy = tu.YearOfStudy WHERE tu.TutorialId = @TutorialId)
        THROW 50231, N'That student is not on this register.', 1;

    MERGE dbo.TutorialAttendance AS tgt
    USING (SELECT @TutorialId AS TutorialId, @StudentId AS StudentId) AS src
       ON tgt.TutorialId = src.TutorialId AND tgt.StudentId = src.StudentId
    WHEN MATCHED THEN UPDATE SET Status = @Status, Method = 'Manual', MarkedAt = dbo.fn_SaNow()
    WHEN NOT MATCHED THEN INSERT (TutorialId, StudentId, Status, Method) VALUES (@TutorialId, @StudentId, @Status, 'Manual');
    SELECT 1 AS Ok;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_Delete @TutorialId INT, @TutorId INT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Tutorials WHERE TutorialId = @TutorialId AND TutorId = @TutorId)
        THROW 50222, N'That tutorial doesn''t exist or isn''t yours.', 1;
    BEGIN TRAN;
    -- tell enrolled students when an upcoming tutorial is cancelled
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT u.UserId, N'Tutorial cancelled: ' + t.Title + N' (' + m.Code + N') on ' + FORMAT(t.StartTime, 'ddd dd MMM HH:mm') + N'.'
    FROM dbo.Tutorials t JOIN dbo.Modules m ON m.ModuleId = t.ModuleId
    JOIN dbo.StudentModules sm ON sm.ModuleId = t.ModuleId JOIN dbo.Users u ON u.UserId = sm.StudentId
    WHERE t.TutorialId = @TutorialId AND t.StartTime > dbo.fn_SaNow() AND u.YearOfStudy = t.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1;
    DELETE a FROM dbo.TutorialAttendance a JOIN dbo.Tutorials t ON t.TutorialId = a.TutorialId
    WHERE a.TutorialId = @TutorialId AND t.TutorId = @TutorId;
    DELETE dbo.Tutorials WHERE TutorialId = @TutorialId AND TutorId = @TutorId;
    COMMIT;
END
GO

/* ---------- Ad-hoc class registers ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_ClassRegister_Create @TutorId INT, @ModuleId INT, @YearOfStudy INT, @Title NVARCHAR(150), @RegisterDate DATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @TutorId AND ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy)
        THROW 50240, N'You are not assigned to that module for that year of study.', 1;
    INSERT dbo.ClassRegisters (TutorId, ModuleId, YearOfStudy, Title, RegisterDate) VALUES (@TutorId, @ModuleId, @YearOfStudy, @Title, @RegisterDate);
    SELECT SCOPE_IDENTITY() AS RegisterId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ClassRegister_List @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT cr.*, m.Code AS ModuleCode, m.Name AS ModuleName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
            WHERE sm.ModuleId = cr.ModuleId AND u.YearOfStudy = cr.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1) AS EnrolledCount,
           (SELECT COUNT(*) FROM dbo.ClassRegisterAttendance a WHERE a.RegisterId = cr.RegisterId AND a.Status IN ('Present','Late')) AS PresentCount
    FROM dbo.ClassRegisters cr JOIN dbo.Modules m ON m.ModuleId = cr.ModuleId
    WHERE cr.TutorId = @TutorId
    ORDER BY cr.RegisterDate DESC, cr.RegisterId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ClassRegister_Get @RegisterId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT cr.*, m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.ClassRegisters cr JOIN dbo.Modules m ON m.ModuleId = cr.ModuleId JOIN dbo.Users t ON t.UserId = cr.TutorId
    WHERE cr.RegisterId = @RegisterId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ClassRegister_Students @RegisterId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId AS StudentId, u.FirstName + ' ' + u.LastName AS StudentName, u.StudentNumber, u.Email, a.Status, a.MarkedAt
    FROM dbo.ClassRegisters cr
    JOIN dbo.StudentModules sm ON sm.ModuleId = cr.ModuleId
    JOIN dbo.Users u ON u.UserId = sm.StudentId AND u.YearOfStudy = cr.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1
    LEFT JOIN dbo.ClassRegisterAttendance a ON a.RegisterId = cr.RegisterId AND a.StudentId = u.UserId
    WHERE cr.RegisterId = @RegisterId
    ORDER BY u.LastName, u.FirstName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ClassRegister_Mark @RegisterId INT, @StudentId INT, @Status NVARCHAR(10), @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegisters WHERE RegisterId = @RegisterId AND TutorId = @TutorId)
        THROW 50241, N'You can only mark your own registers.', 1;
    MERGE dbo.ClassRegisterAttendance AS tgt
    USING (SELECT @RegisterId AS RegisterId, @StudentId AS StudentId) AS src
       ON tgt.RegisterId = src.RegisterId AND tgt.StudentId = src.StudentId
    WHEN MATCHED THEN UPDATE SET Status = @Status, MarkedAt = dbo.fn_SaNow()
    WHEN NOT MATCHED THEN INSERT (RegisterId, StudentId, Status) VALUES (@RegisterId, @StudentId, @Status);
    SELECT 1 AS Ok;
END
GO

/* ---------- Course content ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Content_Add
    @ModuleId INT, @YearOfStudy INT, @Title NVARCHAR(150), @Description NVARCHAR(1000),
    @FileName NVARCHAR(260), @FilePath NVARCHAR(400), @UploadedBy INT, @UploaderRole NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    IF @UploaderRole = 'Lecturer' AND NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE ModuleId = @ModuleId AND LecturerId = @UploadedBy)
        THROW 50252, N'You can only upload content to modules you lecture.', 1;
    IF @UploaderRole = 'Tutor' AND NOT EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @UploadedBy AND ModuleId = @ModuleId)
        THROW 50250, N'You can only upload content for modules you tutor.', 1;
    IF @UploaderRole = 'Lecturer' AND NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE ModuleId = @ModuleId AND LecturerId = @UploadedBy)
        THROW 50251, N'You can only upload content for modules you lecture.', 1;
    INSERT dbo.CourseContent (ModuleId, YearOfStudy, Title, Description, FileName, FilePath, UploadedBy, UploaderRole)
    VALUES (@ModuleId, @YearOfStudy, @Title, @Description, @FileName, @FilePath, @UploadedBy, @UploaderRole);
    DECLARE @NewId INT = SCOPE_IDENTITY();
    -- Lecturer material is published straight away: tell the students and tutors of that module (and year).
    IF @UploaderRole = 'Lecturer'
    BEGIN
        INSERT dbo.ActivityLog (UserId, Message, Link)
        SELECT u.UserId, N'New course material in ' + m.Code + N': ' + @Title, N'/Student/Content'
        FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId
        WHERE sm.ModuleId = @ModuleId AND u.Role = 'Student' AND u.IsActive = 1 AND (@YearOfStudy IS NULL OR u.YearOfStudy = @YearOfStudy);
        INSERT dbo.ActivityLog (UserId, Message, Link)
        SELECT DISTINCT tm.TutorId, N'New course material in ' + m.Code + N' from your lecturer: ' + @Title, N'/Tutor/Content'
        FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
        WHERE tm.ModuleId = @ModuleId AND (@YearOfStudy IS NULL OR tm.YearOfStudy = @YearOfStudy);
    END
    ELSE
    BEGIN
        -- a tutor's upload waits for the lecturer to approve it before students see it
        INSERT dbo.ActivityLog (UserId, Message, Link)
        SELECT m.LecturerId, N'Course material to review in ' + m.Code + N': ' + @Title, N'/Lecturer/Content'
        FROM dbo.Modules m WHERE m.ModuleId = @ModuleId AND m.LecturerId IS NOT NULL;
    END
    SELECT @NewId AS ContentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Content_ForTutor @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.*, m.Code AS ModuleCode, m.Name AS ModuleName, u.FirstName + ' ' + u.LastName AS UploaderName,
           r.FirstName + ' ' + r.LastName AS ReviewedByName
    FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
    LEFT JOIN dbo.Users u ON u.UserId = c.UploadedBy LEFT JOIN dbo.Users r ON r.UserId = c.ReviewedBy
    WHERE EXISTS (SELECT 1 FROM dbo.TutorModules tm WHERE tm.TutorId = @TutorId AND tm.ModuleId = c.ModuleId
                  AND (c.YearOfStudy IS NULL OR c.YearOfStudy = tm.YearOfStudy))
    ORDER BY c.IsReviewed, c.UploadedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Content_Get @ContentId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.*, m.Code AS ModuleCode, m.Name AS ModuleName, m.LecturerId
    FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId WHERE c.ContentId = @ContentId;
END
GO

-- Tutors review lecturer uploads; lecturers review tutor uploads.
CREATE OR ALTER PROCEDURE dbo.sp_Content_MarkReviewed @ContentId INT, @UserId INT, @Role NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c SET IsReviewed = 1, ReviewedBy = @UserId, ReviewedAt = dbo.fn_SaNow()
    FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
    WHERE c.ContentId = @ContentId AND c.IsReviewed = 0
      AND ((@Role = 'Tutor' AND c.UploaderRole = 'Lecturer'
            AND EXISTS (SELECT 1 FROM dbo.TutorModules tm WHERE tm.TutorId = @UserId AND tm.ModuleId = c.ModuleId))
        OR (@Role = 'Lecturer' AND c.UploaderRole = 'Tutor' AND m.LecturerId = @UserId));
    DECLARE @Affected INT = @@ROWCOUNT;
    -- a tutor's material approved by the lecturer is now visible to students: tell them
    IF @Affected = 1 AND @Role = 'Lecturer'
        INSERT dbo.ActivityLog (UserId, Message, Link)
        SELECT u.UserId, N'New course material in ' + m.Code + N': ' + c.Title, N'/Student/Content'
        FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
        JOIN dbo.StudentModules sm ON sm.ModuleId = c.ModuleId JOIN dbo.Users u ON u.UserId = sm.StudentId
        WHERE c.ContentId = @ContentId AND u.Role = 'Student' AND u.IsActive = 1 AND (c.YearOfStudy IS NULL OR u.YearOfStudy = c.YearOfStudy);
    SELECT @Affected AS Affected;
END
GO

/* ---------- Announcements ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Announcement_Create @TutorId INT, @ModuleId INT, @YearOfStudy INT, @Title NVARCHAR(150), @Message NVARCHAR(2000) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @TutorId AND ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy)
        THROW 50260, N'You are not assigned to that module for that year of study.', 1;
    BEGIN TRAN;
    INSERT dbo.Announcements (TutorId, ModuleId, YearOfStudy, Title, Message) VALUES (@TutorId, @ModuleId, @YearOfStudy, @Title, @Message);
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT u.UserId, N'Announcement in ' + m.Code + N': ' + @Title
    FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId
    WHERE sm.ModuleId = @ModuleId AND u.YearOfStudy = @YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1;
    COMMIT;
    -- recipients, for optional email
    SELECT u.Email, u.FirstName FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
    WHERE sm.ModuleId = @ModuleId AND u.YearOfStudy = @YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1 AND u.EmailNotifications = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Announcements @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.*, m.Code AS ModuleCode, m.Name AS ModuleName FROM dbo.Announcements a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId
    WHERE a.TutorId = @TutorId ORDER BY a.CreatedAt DESC;
END
GO

/* ---------- Reports & banking ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutor_Report @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM dbo.Bookings WHERE TutorId = @TutorId) AS TotalSessions,
        (SELECT COUNT(*) FROM dbo.Bookings WHERE TutorId = @TutorId AND Status = 'Completed') AS CompletedSessions,
        (SELECT ISNULL(SUM(Hours), 0) FROM dbo.Bookings WHERE TutorId = @TutorId AND Status = 'Completed') AS HoursTutored,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payouts WHERE TutorId = @TutorId AND Status = 'Sent') AS PaidOut,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payouts WHERE TutorId = @TutorId AND Status <> 'Sent') AS PayoutsPending,
        (SELECT COUNT(*) FROM dbo.Tutorials WHERE TutorId = @TutorId) AS TutorialsRun;

    SELECT m.Code AS ModuleCode, m.Name AS ModuleName, COUNT(*) AS Sessions,
           SUM(CASE WHEN b.Status = 'Completed' THEN b.Hours ELSE 0 END) AS Hours
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId
    WHERE b.TutorId = @TutorId GROUP BY m.Code, m.Name ORDER BY m.Code;

    SELECT b.*, m.Code AS ModuleCode, m.Name AS ModuleName, s.FirstName + ' ' + s.LastName AS StudentName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId
    WHERE b.TutorId = @TutorId ORDER BY b.StartTime DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Banking_Get @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.BankingDetails WHERE TutorId = @TutorId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Banking_Save
    @TutorId INT, @BankName NVARCHAR(80), @AccountHolder NVARCHAR(120), @AccountType NVARCHAR(30),
    @BranchCode NVARCHAR(10), @AccountNumberProtected NVARCHAR(512), @AccountLast4 NVARCHAR(4)
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.BankingDetails AS tgt USING (SELECT @TutorId AS TutorId) AS src ON tgt.TutorId = src.TutorId
    WHEN MATCHED THEN UPDATE SET BankName = @BankName, AccountHolder = @AccountHolder, AccountType = @AccountType,
        BranchCode = @BranchCode, AccountNumberProtected = @AccountNumberProtected, AccountLast4 = @AccountLast4, UpdatedAt = dbo.fn_SaNow()
    WHEN NOT MATCHED THEN INSERT (TutorId, BankName, AccountHolder, AccountType, BranchCode, AccountNumberProtected, AccountLast4)
        VALUES (@TutorId, @BankName, @AccountHolder, @AccountType, @BranchCode, @AccountNumberProtected, @AccountLast4);
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@TutorId, N'Your banking details were updated.');
END
GO

/* ---------- Payouts ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Payout_Create @PaymentId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Payouts WHERE PaymentId = @PaymentId)
        INSERT dbo.Payouts (PaymentId, TutorId, Amount)
        SELECT PaymentId, TutorId, Amount FROM dbo.Payments WHERE PaymentId = @PaymentId AND Status = 'Complete';

    SELECT po.PayoutId, po.PaymentId, po.TutorId, po.Amount, po.Status, p.PaymentRef,
           t.FirstName + ' ' + t.LastName AS TutorName,
           bd.BankName, bd.AccountHolder, bd.AccountType, bd.BranchCode, bd.AccountNumberProtected, bd.AccountLast4
    FROM dbo.Payouts po JOIN dbo.Payments p ON p.PaymentId = po.PaymentId JOIN dbo.Users t ON t.UserId = po.TutorId
    LEFT JOIN dbo.BankingDetails bd ON bd.TutorId = po.TutorId
    WHERE po.PaymentId = @PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payout_Update @PayoutId INT, @Status NVARCHAR(20), @ProviderReference NVARCHAR(100), @Message NVARCHAR(500) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Payouts SET Status = @Status, ProviderReference = @ProviderReference, Message = @Message, ProcessedAt = dbo.fn_SaNow()
    WHERE PayoutId = @PayoutId;
    IF @Status = 'Sent'
        INSERT dbo.ActivityLog (UserId, Message)
        SELECT TutorId, N'Payout of R' + CONVERT(NVARCHAR(20), Amount) + N' sent to your bank account.' FROM dbo.Payouts WHERE PayoutId = @PayoutId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payout_PendingForTutor @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT PaymentId FROM dbo.Payouts WHERE TutorId = @TutorId AND Status IN ('Queued','NoBankDetails','Failed');
END
GO

/* =========================== LECTURER =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Modules @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester, m.Description, m.LecturerId, m.TutorsNeeded,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId) AS TutorCount,
           (SELECT COUNT(*) FROM dbo.StudentModules sm WHERE sm.ModuleId = m.ModuleId) AS StudentCount,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s
             WHERE s.IsBooked = 0 AND s.StartTime > dbo.fn_SaNow()
               AND s.TutorId IN (SELECT tm.TutorId FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId)) AS OpenSlots
    FROM dbo.Modules m WHERE m.LecturerId = @LecturerId ORDER BY m.YearLevel, m.Code;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Dashboard @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(DISTINCT cm.CourseId) FROM dbo.CourseModules cm JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId WHERE m.LecturerId = @LecturerId) AS CourseCount,
        (SELECT COUNT(*) FROM dbo.Modules WHERE LecturerId = @LecturerId) AS ModuleCount,
        (SELECT COUNT(*) FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId WHERE m.LecturerId = @LecturerId) AS AssignmentCount,
        (SELECT COUNT(*) FROM dbo.TutorApplications WHERE AssignedLecturerId = @LecturerId AND Status = 'UnderReview') AS ApplicationsToReview,
        (SELECT COUNT(*) FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
         WHERE m.LecturerId = @LecturerId AND c.UploaderRole = 'Tutor' AND c.IsReviewed = 0) AS ContentToReview;

    SELECT TOP 12 * FROM (
        SELECT N'Tutorial "' + tu.Title + N'" scheduled for ' + m.Code + N' by ' + u.FirstName + N' ' + u.LastName AS Message, tu.CreatedAt
        FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users u ON u.UserId = tu.TutorId WHERE m.LecturerId = @LecturerId
        UNION ALL
        SELECT u.FirstName + N' ' + u.LastName + N' uploaded "' + c.Title + N'" to ' + m.Code, c.UploadedAt
        FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId JOIN dbo.Users u ON u.UserId = c.UploadedBy WHERE m.LecturerId = @LecturerId
        UNION ALL
        SELECT u.FirstName + N' ' + u.LastName + N' assigned to ' + m.Code + N' (year ' + CAST(tm.YearOfStudy AS NVARCHAR(2)) + N')', tm.AssignedAt
        FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId JOIN dbo.Users u ON u.UserId = tm.TutorId WHERE m.LecturerId = @LecturerId
        UNION ALL
        SELECT N'Application from ' + a.FirstName + N' ' + a.LastName + N' assigned to you for review', a.SubmittedAt
        FROM dbo.TutorApplications a WHERE a.AssignedLecturerId = @LecturerId
    ) x ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Tutor_List AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, FirstName, LastName, Email, StudentNumber FROM dbo.Users WHERE Role = 'Tutor' AND IsActive = 1 ORDER BY FirstName, LastName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_TutorModule_Assign @TutorId INT, @ModuleId INT, @YearOfStudy INT, @AssignedBy INT, @AssignedByRole NVARCHAR(20) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @AssignedByRole = 'Lecturer' AND NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE ModuleId = @ModuleId AND LecturerId = @AssignedBy)
        THROW 50301, N'You can only assign tutors to modules you lecture.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @TutorId AND Role = 'Tutor' AND IsActive = 1)
        THROW 50302, N'Choose an active tutor.', 1;
    IF @YearOfStudy NOT BETWEEN 1 AND 7 THROW 50303, N'Choose a valid year of study.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @TutorId AND ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy)
        THROW 50304, N'That tutor is already assigned to this module for that year.', 1;
    DECLARE @Needed INT = (SELECT TutorsNeeded FROM dbo.Modules WHERE ModuleId = @ModuleId);
    IF (SELECT COUNT(*) FROM dbo.TutorModules WHERE ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy) >= @Needed
        THROW 50305, N'This module already has the number of tutors you asked for. Increase "tutors needed" first.', 1;

    BEGIN TRAN;
    INSERT dbo.TutorModules (TutorId, ModuleId, YearOfStudy, AssignedBy) VALUES (@TutorId, @ModuleId, @YearOfStudy, @AssignedBy);
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT @TutorId, N'You were assigned to tutor ' + Code + N' for year ' + CAST(@YearOfStudy AS NVARCHAR(2)) + N' students.'
    FROM dbo.Modules WHERE ModuleId = @ModuleId;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Assignments @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT tm.TutorModuleId, tm.TutorId, tm.ModuleId, tm.YearOfStudy, tm.AssignedAt, m.Code AS ModuleCode, m.Name AS ModuleName,
           t.FirstName + ' ' + t.LastName AS TutorName, t.Email AS TutorEmail, a.FirstName + ' ' + a.LastName AS AssignedByName
    FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId JOIN dbo.Users t ON t.UserId = tm.TutorId
    LEFT JOIN dbo.Users a ON a.UserId = tm.AssignedBy
    WHERE m.LecturerId = @LecturerId OR @LecturerId IS NULL
    ORDER BY m.Code, tm.YearOfStudy;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_TutorModule_Remove @TutorModuleId INT, @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    DELETE tm FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    WHERE tm.TutorModuleId = @TutorModuleId AND m.LecturerId = @LecturerId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Content @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.*, m.Code AS ModuleCode, m.Name AS ModuleName, u.FirstName + ' ' + u.LastName AS UploaderName,
           r.FirstName + ' ' + r.LastName AS ReviewedByName
    FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
    LEFT JOIN dbo.Users u ON u.UserId = c.UploadedBy LEFT JOIN dbo.Users r ON r.UserId = c.ReviewedBy
    WHERE m.LecturerId = @LecturerId
    ORDER BY c.IsReviewed, c.UploadedAt DESC;
END
GO

/* Attendance: @LecturerId NULL = system wide (admin) */
CREATE OR ALTER PROCEDURE dbo.sp_Attendance_Records @LecturerId INT = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Tutorial' AS Source, tu.TutorialId AS SourceId, tu.Title, m.Code AS ModuleCode, m.Name AS ModuleName,
           s.UserId AS StudentId, s.FirstName + ' ' + s.LastName AS StudentName, s.StudentNumber,
           t.FirstName + ' ' + t.LastName AS TutorName, tu.StartTime AS [Date], a.Status
    FROM dbo.TutorialAttendance a JOIN dbo.Tutorials tu ON tu.TutorialId = a.TutorialId
    JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users s ON s.UserId = a.StudentId JOIN dbo.Users t ON t.UserId = tu.TutorId
    WHERE @LecturerId IS NULL OR m.LecturerId = @LecturerId
    UNION ALL
    SELECT 'Class register', cr.RegisterId, cr.Title, m.Code, m.Name, s.UserId, s.FirstName + ' ' + s.LastName, s.StudentNumber,
           t.FirstName + ' ' + t.LastName, CAST(cr.RegisterDate AS DATETIME2), a.Status
    FROM dbo.ClassRegisterAttendance a JOIN dbo.ClassRegisters cr ON cr.RegisterId = a.RegisterId
    JOIN dbo.Modules m ON m.ModuleId = cr.ModuleId JOIN dbo.Users s ON s.UserId = a.StudentId JOIN dbo.Users t ON t.UserId = cr.TutorId
    WHERE @LecturerId IS NULL OR m.LecturerId = @LecturerId
    UNION ALL
    SELECT 'Session', b.BookingId, N'One-on-one session', m.Code, m.Name, s.UserId, s.FirstName + ' ' + s.LastName, s.StudentNumber,
           t.FirstName + ' ' + t.LastName, b.StartTime, b.AttendanceStatus
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId JOIN dbo.Users s ON s.UserId = b.StudentId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.AttendanceStatus IS NOT NULL AND (@LecturerId IS NULL OR m.LecturerId = @LecturerId)
    ORDER BY [Date] DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Progress @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Att AS (
        SELECT a.StudentId, tu.ModuleId, a.Status FROM dbo.TutorialAttendance a JOIN dbo.Tutorials tu ON tu.TutorialId = a.TutorialId
        UNION ALL
        SELECT a.StudentId, cr.ModuleId, a.Status FROM dbo.ClassRegisterAttendance a JOIN dbo.ClassRegisters cr ON cr.RegisterId = a.RegisterId
    )
    SELECT u.UserId AS StudentId, u.FirstName + ' ' + u.LastName AS StudentName, u.StudentNumber, u.YearOfStudy,
           m.Code AS ModuleCode, COUNT(att.Status) AS Total,
           SUM(CASE WHEN att.Status = 'Present' THEN 1 ELSE 0 END) AS Present,
           SUM(CASE WHEN att.Status = 'Late' THEN 1 ELSE 0 END) AS Late,
           SUM(CASE WHEN att.Status = 'Absent' THEN 1 ELSE 0 END) AS Absent,
           CAST(CASE WHEN COUNT(att.Status) = 0 THEN 0
                ELSE 100.0 * SUM(CASE WHEN att.Status IN ('Present','Late') THEN 1 ELSE 0 END) / COUNT(att.Status) END AS DECIMAL(5,1)) AS Rate
    FROM dbo.StudentModules sm JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId JOIN dbo.Users u ON u.UserId = sm.StudentId
    LEFT JOIN Att att ON att.StudentId = sm.StudentId AND att.ModuleId = sm.ModuleId
    WHERE m.LecturerId = @LecturerId AND u.Role = 'Student' AND u.IsActive = 1
    GROUP BY u.UserId, u.FirstName, u.LastName, u.StudentNumber, u.YearOfStudy, m.Code
    ORDER BY m.Code, Rate;

    SELECT t.UserId AS TutorId, t.FirstName + ' ' + t.LastName AS TutorName,
           (SELECT COUNT(*) FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId WHERE b.TutorId = t.UserId AND m.LecturerId = @LecturerId) AS Sessions,
           (SELECT ISNULL(SUM(b.Hours), 0) FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId WHERE b.TutorId = t.UserId AND m.LecturerId = @LecturerId AND b.Status = 'Completed') AS Hours,
           (SELECT COUNT(*) FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId WHERE tu.TutorId = t.UserId AND m.LecturerId = @LecturerId) AS Tutorials,
           (SELECT COUNT(*) FROM dbo.ClassRegisters cr JOIN dbo.Modules m ON m.ModuleId = cr.ModuleId WHERE cr.TutorId = t.UserId AND m.LecturerId = @LecturerId) AS Registers,
           (SELECT COUNT(*) FROM dbo.Announcements an JOIN dbo.Modules m ON m.ModuleId = an.ModuleId WHERE an.TutorId = t.UserId AND m.LecturerId = @LecturerId) AS Announcements
    FROM dbo.Users t
    WHERE t.Role = 'Tutor' AND EXISTS (SELECT 1 FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
                                       WHERE tm.TutorId = t.UserId AND m.LecturerId = @LecturerId)
    ORDER BY TutorName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Tutorials @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT tu.*, m.Code AS ModuleCode, m.Name AS ModuleName, t.FirstName + ' ' + t.LastName AS TutorName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
            WHERE sm.ModuleId = tu.ModuleId AND u.YearOfStudy = tu.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1) AS EnrolledCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Present') AS PresentCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Late') AS LateCount,
           (SELECT COUNT(*) FROM dbo.TutorialAttendance a WHERE a.TutorialId = tu.TutorialId AND a.Status = 'Absent') AS AbsentCount
    FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId JOIN dbo.Users t ON t.UserId = tu.TutorId
    WHERE m.LecturerId = @LecturerId
    ORDER BY tu.StartTime DESC;
END
GO

/* =========================== TUTOR APPLICATIONS =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_Application_Create
    @FirstName NVARCHAR(60), @LastName NVARCHAR(60), @StudentNumber NVARCHAR(20), @Email NVARCHAR(150), @PersonalEmail NVARCHAR(150),
    @Phone NVARCHAR(20), @CourseId INT, @CurrentYear INT, @ModuleId INT, @TutorYear INT, @ModuleMark DECIMAL(5,2),
    @Motivation NVARCHAR(2000), @IdDocumentPath NVARCHAR(400), @TranscriptPath NVARCHAR(400), @CvPath NVARCHAR(400) = NULL
AS
BEGIN
    IF NULLIF(@IdDocumentPath, N'') IS NULL OR NULLIF(@TranscriptPath, N'') IS NULL OR NULLIF(@CvPath, N'') IS NULL
        THROW 50405, N'Upload your student card, your academic transcript and your CV.', 1;
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.TutorApplications WHERE Email = @Email AND ModuleId = @ModuleId AND TutorYear = @TutorYear AND Status <> 'Rejected')
        THROW 50401, N'You already have an open application for this module. Check its status instead.', 1;
    IF @ModuleMark < 0 OR @ModuleMark > 100 THROW 50402, N'Enter your module mark as a percentage between 0 and 100.', 1;

    INSERT dbo.TutorApplications (StudentId, FirstName, LastName, StudentNumber, Email, PersonalEmail, Phone, CourseId, CurrentYear,
        ModuleId, TutorYear, ModuleMark, Motivation, IdDocumentPath, TranscriptPath, CvPath)
    VALUES ((SELECT UserId FROM dbo.Users WHERE Email = @Email AND Role = 'Student'), @FirstName, @LastName, @StudentNumber, @Email,
        @PersonalEmail, @Phone, @CourseId, @CurrentYear, @ModuleId, @TutorYear, @ModuleMark, @Motivation, @IdDocumentPath, @TranscriptPath, @CvPath);
    DECLARE @Id INT = SCOPE_IDENTITY();
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT UserId, N'Your tutor application was submitted.' FROM dbo.Users WHERE Email = @Email AND Role = 'Student';
    SELECT @Id AS ApplicationId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Application_Select @ApplicationId INT = NULL, @Email NVARCHAR(150) = NULL, @LecturerId INT = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.*, c.Name AS CourseName, m.Code AS ModuleCode, m.Name AS ModuleName, l.FirstName + ' ' + l.LastName AS LecturerName
    FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId
    LEFT JOIN dbo.Courses c ON c.CourseId = a.CourseId LEFT JOIN dbo.Users l ON l.UserId = a.AssignedLecturerId
    WHERE (@ApplicationId IS NULL OR a.ApplicationId = @ApplicationId)
      AND (@Email IS NULL OR a.Email = @Email OR a.PersonalEmail = @Email)
      AND (@LecturerId IS NULL OR a.AssignedLecturerId = @LecturerId)
    ORDER BY CASE a.Status WHEN 'Recommended' THEN 0 WHEN 'NotRecommended' THEN 0 WHEN 'Submitted' THEN 1 WHEN 'UnderReview' THEN 2 ELSE 3 END,
             a.SubmittedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Application_AssignLecturer @ApplicationId INT, @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @LecturerId AND Role = 'Lecturer' AND IsActive = 1)
        THROW 50410, N'Choose an active lecturer.', 1;
    UPDATE dbo.TutorApplications SET AssignedLecturerId = @LecturerId, Status = 'UnderReview',
        LecturerRecommendation = NULL, LecturerComment = NULL, RecommendedAt = NULL
    WHERE ApplicationId = @ApplicationId AND Status NOT IN ('Approved','Rejected');
    IF @@ROWCOUNT = 0 THROW 50411, N'This application has already been decided.', 1;
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT @LecturerId, N'Please review the tutor application from ' + FirstName + N' ' + LastName + N'.'
    FROM dbo.TutorApplications WHERE ApplicationId = @ApplicationId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Application_Recommend @ApplicationId INT, @LecturerId INT, @Recommendation NVARCHAR(10), @Comment NVARCHAR(1000) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.TutorApplications
    SET LecturerRecommendation = @Recommendation, LecturerComment = @Comment, RecommendedAt = dbo.fn_SaNow(),
        Status = CASE WHEN @Recommendation = 'Approve' THEN 'Recommended' ELSE 'NotRecommended' END
    WHERE ApplicationId = @ApplicationId AND AssignedLecturerId = @LecturerId AND Status IN ('UnderReview','Recommended','NotRecommended');
    SELECT @@ROWCOUNT AS Affected;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Application_Approve @ApplicationId INT, @PasswordHash NVARCHAR(100), @Feedback NVARCHAR(1000) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Email NVARCHAR(150), @Personal NVARCHAR(150), @First NVARCHAR(60), @Last NVARCHAR(60), @StudentNumber NVARCHAR(20),
            @Phone NVARCHAR(20), @CourseId INT, @Year INT, @Status NVARCHAR(20), @StudentId INT;
    BEGIN TRAN;
    SELECT @Email = Email, @Personal = PersonalEmail, @First = FirstName, @Last = LastName, @StudentNumber = StudentNumber,
           @Phone = Phone, @CourseId = CourseId, @Year = CurrentYear, @Status = Status, @StudentId = StudentId
    FROM dbo.TutorApplications WITH (UPDLOCK) WHERE ApplicationId = @ApplicationId;
    IF @Status IS NULL THROW 50420, N'Application not found.', 1;
    IF @Status IN ('Approved','Rejected') THROW 50421, N'This application has already been decided.', 1;

    DECLARE @TutorId INT = (SELECT UserId FROM dbo.Users WHERE Email = @Email AND Role = 'Tutor'), @Created BIT = 0;
    IF @TutorId IS NULL
    BEGIN
        INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, StudentNumber, Phone, CourseId, YearOfStudy, PersonalEmail)
        VALUES (@First, @Last, @Email, @PasswordHash, 'Tutor', @StudentNumber, @Phone, @CourseId, @Year, @Personal);
        SET @TutorId = SCOPE_IDENTITY(); SET @Created = 1;
        INSERT dbo.ActivityLog (UserId, Message) VALUES (@TutorId, N'Welcome to the tutor team. Add your banking details and availability to get started.');
    END
    ELSE UPDATE dbo.Users SET IsActive = 1 WHERE UserId = @TutorId;

    UPDATE dbo.TutorApplications SET Status = 'Approved', AdminFeedback = @Feedback, DecidedAt = dbo.fn_SaNow() WHERE ApplicationId = @ApplicationId;
    IF @StudentId IS NOT NULL
        INSERT dbo.ActivityLog (UserId, Message) VALUES (@StudentId, N'Your tutor application was approved. Check your personal email for login details.');
    COMMIT;
    SELECT @TutorId AS TutorId, @Created AS Created, @Email AS LoginEmail, @Personal AS PersonalEmail, @First AS FirstName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Application_Reject @ApplicationId INT, @Feedback NVARCHAR(1000) AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.TutorApplications SET Status = 'Rejected', AdminFeedback = @Feedback, DecidedAt = dbo.fn_SaNow()
    WHERE ApplicationId = @ApplicationId AND Status NOT IN ('Approved','Rejected');
    IF @@ROWCOUNT = 0 THROW 50430, N'This application has already been decided.', 1;
    INSERT dbo.ActivityLog (UserId, Message)
    SELECT StudentId, N'Your tutor application was not successful this time. See the feedback on the status page.'
    FROM dbo.TutorApplications WHERE ApplicationId = @ApplicationId AND StudentId IS NOT NULL;
    SELECT PersonalEmail, FirstName FROM dbo.TutorApplications WHERE ApplicationId = @ApplicationId;
END
GO

/* =========================== ADMIN =========================== */
CREATE OR ALTER PROCEDURE dbo.sp_Admin_Dashboard AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Student' AND IsActive = 1) AS Students,
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Tutor' AND IsActive = 1) AS Tutors,
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Lecturer' AND IsActive = 1) AS Lecturers,
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Admin' AND IsActive = 1) AS Admins,
        (SELECT COUNT(*) FROM dbo.Courses) AS Courses,
        (SELECT COUNT(*) FROM dbo.Modules) AS Modules,
        (SELECT COUNT(*) FROM dbo.TutorApplications WHERE Status NOT IN ('Approved','Rejected')) AS OpenApplications,
        (SELECT COUNT(*) FROM dbo.Bookings) AS Bookings,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payments WHERE Status = 'Complete') AS Revenue,
        (SELECT COUNT(*) FROM dbo.Payouts WHERE Status <> 'Sent') AS PayoutsNotSent;

    SELECT TOP 10 a.Message, a.CreatedAt, u.FirstName + ' ' + u.LastName AS UserName, u.Role
    FROM dbo.ActivityLog a LEFT JOIN dbo.Users u ON u.UserId = a.UserId ORDER BY a.CreatedAt DESC;

    SELECT TOP 5 a.ApplicationId, a.FirstName, a.LastName, a.Status, a.SubmittedAt, m.Code AS ModuleCode
    FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId
    WHERE a.Status NOT IN ('Approved','Rejected') ORDER BY a.SubmittedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_List @Role NVARCHAR(20) = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.StudentNumber, u.Phone, u.CourseId, u.YearOfStudy,
           u.PersonalEmail, u.IsActive, u.CreatedAt, c.Name AS CourseName, c.Code AS CourseCode,
           d.DepartmentId, d.Name AS DepartmentName, f.FacultyId, f.Name AS FacultyName,
           u.DeletionRequestedAt, u.LockoutUntil,
           -- Students and tutors belong to their own course; lecturers to every course their modules are part of.
           CASE WHEN u.Role = 'Lecturer' THEN STUFF((SELECT DISTINCT N',' + CAST(cm.CourseId AS NVARCHAR(10)) FROM dbo.Modules lm
                    JOIN dbo.CourseModules cm ON cm.ModuleId = lm.ModuleId WHERE lm.LecturerId = u.UserId
                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'')
                ELSE CAST(u.CourseId AS NVARCHAR(10)) END AS CourseIds,
           CASE WHEN u.Role = 'Lecturer' THEN STUFF((SELECT DISTINCT N',' + CAST(lc.DepartmentId AS NVARCHAR(10)) FROM dbo.Modules lm
                    JOIN dbo.CourseModules cm ON cm.ModuleId = lm.ModuleId JOIN dbo.Courses lc ON lc.CourseId = cm.CourseId
                    WHERE lm.LecturerId = u.UserId AND lc.DepartmentId IS NOT NULL FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'')
                ELSE CAST(d.DepartmentId AS NVARCHAR(10)) END AS DepartmentIds,
           CASE WHEN u.Role = 'Lecturer' THEN STUFF((SELECT DISTINCT N',' + CAST(ld.FacultyId AS NVARCHAR(10)) FROM dbo.Modules lm
                    JOIN dbo.CourseModules cm ON cm.ModuleId = lm.ModuleId JOIN dbo.Courses lc ON lc.CourseId = cm.CourseId
                    JOIN dbo.Departments ld ON ld.DepartmentId = lc.DepartmentId
                    WHERE lm.LecturerId = u.UserId FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'')
                ELSE CAST(f.FacultyId AS NVARCHAR(10)) END AS FacultyIds
    FROM dbo.Users u
    LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    LEFT JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
    LEFT JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    WHERE @Role IS NULL OR u.Role = @Role
    ORDER BY u.Role, u.FirstName, u.LastName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_Update
    @UserId INT, @FirstName NVARCHAR(60), @LastName NVARCHAR(60), @Email NVARCHAR(150), @StudentNumber NVARCHAR(20),
    @Phone NVARCHAR(20), @CourseId INT, @YearOfStudy INT, @PersonalEmail NVARCHAR(150), @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Role NVARCHAR(20), @OldCourse INT, @OldYear INT;
    SELECT @Role = Role, @OldCourse = CourseId, @OldYear = YearOfStudy FROM dbo.Users WHERE UserId = @UserId;
    IF @Role IS NULL THROW 50501, N'User not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND Role = @Role AND UserId <> @UserId)
        THROW 50502, N'Another account in this role already uses that email.', 1;

    BEGIN TRAN;
    UPDATE dbo.Users SET FirstName = @FirstName, LastName = @LastName, Email = @Email, StudentNumber = @StudentNumber,
        Phone = @Phone, CourseId = @CourseId, YearOfStudy = @YearOfStudy, PersonalEmail = @PersonalEmail, IsActive = @IsActive
    WHERE UserId = @UserId;

    -- A student who changes course or year gets that course/year's modules.
    IF @Role = 'Student' AND (ISNULL(@OldCourse, -1) <> ISNULL(@CourseId, -1) OR ISNULL(@OldYear, -1) <> ISNULL(@YearOfStudy, -1))
    BEGIN
        DELETE dbo.StudentModules WHERE StudentId = @UserId;
        INSERT dbo.StudentModules (StudentId, ModuleId)
        SELECT @UserId, cm.ModuleId FROM dbo.CourseModules cm JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId
        WHERE cm.CourseId = @CourseId AND m.YearLevel = @YearOfStudy;
    END
    COMMIT;
END
GO

/* Hard delete when the user has no financial history; otherwise deactivate so payment records stay intact. */
CREATE OR ALTER PROCEDURE dbo.sp_User_Delete @UserId INT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF EXISTS (SELECT 1 FROM dbo.Payments WHERE StudentId = @UserId OR TutorId = @UserId)
    BEGIN
        UPDATE dbo.Users SET IsActive = 0, DeletionRequestedAt = NULL WHERE UserId = @UserId;
        UPDATE dbo.ApiTokens SET RevokedAt = dbo.fn_SaNow() WHERE UserId = @UserId AND RevokedAt IS NULL;
        DELETE dbo.TutorModules WHERE TutorId = @UserId;
        DELETE s FROM dbo.AvailabilitySlots s WHERE s.TutorId = @UserId AND s.IsBooked = 0
            AND NOT EXISTS (SELECT 1 FROM dbo.Payments p WHERE p.SlotId = s.SlotId)
            AND NOT EXISTS (SELECT 1 FROM dbo.Bookings b WHERE b.SlotId = s.SlotId);
        SELECT 'Deactivated' AS Result;
        RETURN;
    END

    BEGIN TRAN;
    DELETE dbo.ActivityLog WHERE UserId = @UserId;
    DELETE dbo.PasswordResetTokens WHERE UserId = @UserId;
    DELETE dbo.ApiTokens WHERE UserId = @UserId;
    DELETE dbo.AiRequests WHERE UserId = @UserId;
    DELETE dbo.StudentModules WHERE StudentId = @UserId;
    DELETE dbo.TutorModules WHERE TutorId = @UserId;
    UPDATE dbo.TutorModules SET AssignedBy = NULL WHERE AssignedBy = @UserId;
    DELETE dbo.TutorialAttendance WHERE StudentId = @UserId
        OR TutorialId IN (SELECT TutorialId FROM dbo.Tutorials WHERE TutorId = @UserId);
    DELETE dbo.Tutorials WHERE TutorId = @UserId;
    DELETE dbo.ClassRegisterAttendance WHERE StudentId = @UserId
        OR RegisterId IN (SELECT RegisterId FROM dbo.ClassRegisters WHERE TutorId = @UserId);
    DELETE dbo.ClassRegisters WHERE TutorId = @UserId;
    DELETE dbo.Announcements WHERE TutorId = @UserId;
    DELETE dbo.Bookings WHERE StudentId = @UserId OR TutorId = @UserId;
    DELETE dbo.AvailabilitySlots WHERE TutorId = @UserId;
    DELETE dbo.BankingDetails WHERE TutorId = @UserId;
    UPDATE dbo.CourseContent SET UploadedBy = NULL WHERE UploadedBy = @UserId;
    UPDATE dbo.CourseContent SET ReviewedBy = NULL WHERE ReviewedBy = @UserId;
    UPDATE dbo.Modules SET LecturerId = NULL WHERE LecturerId = @UserId;
    UPDATE dbo.TutorApplications SET StudentId = NULL WHERE StudentId = @UserId;
    UPDATE dbo.TutorApplications SET AssignedLecturerId = NULL, Status = 'Submitted'
        WHERE AssignedLecturerId = @UserId AND Status IN ('UnderReview');
    UPDATE dbo.TutorApplications SET AssignedLecturerId = NULL WHERE AssignedLecturerId = @UserId;
    DELETE dbo.Users WHERE UserId = @UserId;
    COMMIT;
    SELECT 'Deleted' AS Result;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_Create
    @FirstName NVARCHAR(60), @LastName NVARCHAR(60), @Email NVARCHAR(150), @PersonalEmail NVARCHAR(150),
    @Phone NVARCHAR(20), @PasswordHash NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND Role = 'Lecturer')
        THROW 50510, N'A lecturer account with this email already exists.', 1;
    INSERT dbo.Users (FirstName, LastName, Email, PasswordHash, Role, Phone, PersonalEmail)
    VALUES (@FirstName, @LastName, @Email, @PasswordHash, 'Lecturer', @Phone, @PersonalEmail);
    DECLARE @Id INT = SCOPE_IDENTITY();
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@Id, N'Welcome to TutorConnect. Your modules will appear once an administrator links them to you.');
    SELECT @Id AS UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_List AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, FirstName, LastName, Email FROM dbo.Users WHERE Role = 'Lecturer' AND IsActive = 1 ORDER BY FirstName, LastName;
END
GO

/* ---------- Modules & courses ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Module_List AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester, m.Description, m.LecturerId, l.FirstName + ' ' + l.LastName AS LecturerName,
           STUFF((SELECT N', ' + c.Code FROM dbo.CourseModules cm JOIN dbo.Courses c ON c.CourseId = cm.CourseId
                  WHERE cm.ModuleId = m.ModuleId ORDER BY c.Code FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'') AS Courses,
           m.TutorsNeeded,
           -- comma lists used by the admin's faculty / department / course filters
           STUFF((SELECT DISTINCT N',' + CAST(cm.CourseId AS NVARCHAR(10)) FROM dbo.CourseModules cm
                  WHERE cm.ModuleId = m.ModuleId FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS CourseIds,
           STUFF((SELECT DISTINCT N',' + CAST(c.DepartmentId AS NVARCHAR(10)) FROM dbo.CourseModules cm JOIN dbo.Courses c ON c.CourseId = cm.CourseId
                  WHERE cm.ModuleId = m.ModuleId AND c.DepartmentId IS NOT NULL FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS DepartmentIds,
           STUFF((SELECT DISTINCT N',' + CAST(d.FacultyId AS NVARCHAR(10)) FROM dbo.CourseModules cm JOIN dbo.Courses c ON c.CourseId = cm.CourseId
                  JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
                  WHERE cm.ModuleId = m.ModuleId FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS FacultyIds,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId) AS TutorCount,
           (SELECT COUNT(*) FROM dbo.StudentModules sm WHERE sm.ModuleId = m.ModuleId) AS StudentCount,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s
             WHERE s.IsBooked = 0 AND s.StartTime > dbo.fn_SaNow()
               AND s.TutorId IN (SELECT tm.TutorId FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId)) AS OpenSlots
    FROM dbo.Modules m LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId
    ORDER BY m.YearLevel, m.Code;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Module_Save
    @ModuleId INT = NULL, @Code NVARCHAR(20), @Name NVARCHAR(150), @YearLevel INT, @Description NVARCHAR(500), @LecturerId INT,
    @Semester INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Modules WHERE Code = @Code AND (@ModuleId IS NULL OR ModuleId <> @ModuleId))
        THROW 50520, N'Another module already uses that code.', 1;
    IF @ModuleId IS NULL
    BEGIN
        IF @Semester NOT IN (1, 2) THROW 50521, N'Choose semester 1 or semester 2.', 1;
        INSERT dbo.Modules (Code, Name, YearLevel, Semester, Description, LecturerId) VALUES (UPPER(@Code), @Name, @YearLevel, @Semester, @Description, @LecturerId);
        SELECT SCOPE_IDENTITY() AS ModuleId;
    END
    ELSE
    BEGIN
        IF @Semester NOT IN (1, 2) THROW 50521, N'Choose semester 1 or semester 2.', 1;
        UPDATE dbo.Modules SET Code = UPPER(@Code), Name = @Name, YearLevel = @YearLevel, Semester = @Semester, Description = @Description, LecturerId = @LecturerId
        WHERE ModuleId = @ModuleId;
        SELECT @ModuleId AS ModuleId;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Module_Delete @ModuleId INT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF EXISTS (SELECT 1 FROM dbo.Payments WHERE ModuleId = @ModuleId) OR EXISTS (SELECT 1 FROM dbo.Tutorials WHERE ModuleId = @ModuleId)
       OR EXISTS (SELECT 1 FROM dbo.ClassRegisters WHERE ModuleId = @ModuleId) OR EXISTS (SELECT 1 FROM dbo.TutorApplications WHERE ModuleId = @ModuleId)
       OR EXISTS (SELECT 1 FROM dbo.Bookings WHERE ModuleId = @ModuleId)
        THROW 50521, N'This module has bookings, tutorials, registers or applications, so it cannot be deleted.', 1;
    BEGIN TRAN;
    DELETE dbo.CourseModules WHERE ModuleId = @ModuleId;
    DELETE dbo.StudentModules WHERE ModuleId = @ModuleId;
    DELETE dbo.TutorModules WHERE ModuleId = @ModuleId;
    DELETE dbo.Announcements WHERE ModuleId = @ModuleId;
    DELETE dbo.CourseContent WHERE ModuleId = @ModuleId;
    DELETE dbo.Modules WHERE ModuleId = @ModuleId;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Course_ListWithModules AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseId, c.Code, c.Name, c.Description, c.DepartmentId, d.Name AS DepartmentName, d.FacultyId, f.Name AS FacultyName,
           (SELECT COUNT(*) FROM dbo.CourseModules cm WHERE cm.CourseId = c.CourseId) AS ModuleCount,
           (SELECT COUNT(*) FROM dbo.Users u WHERE u.CourseId = c.CourseId AND u.Role = 'Student' AND u.IsActive = 1) AS StudentCount
    FROM dbo.Courses c
    LEFT JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
    LEFT JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    ORDER BY f.Name, d.Name, c.Name;

    SELECT cm.CourseId, m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester
    FROM dbo.CourseModules cm JOIN dbo.Modules m ON m.ModuleId = cm.ModuleId ORDER BY m.YearLevel, m.Code;
END
GO

-- Links a module and registers it for existing students of that course at the module's year level.
CREATE OR ALTER PROCEDURE dbo.sp_CourseModule_Link @CourseId INT, @ModuleId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.CourseModules WHERE CourseId = @CourseId AND ModuleId = @ModuleId)
        INSERT dbo.CourseModules (CourseId, ModuleId) VALUES (@CourseId, @ModuleId);
    INSERT dbo.StudentModules (StudentId, ModuleId)
    SELECT u.UserId, @ModuleId FROM dbo.Users u JOIN dbo.Modules m ON m.ModuleId = @ModuleId
    WHERE u.Role = 'Student' AND u.CourseId = @CourseId AND u.YearOfStudy = m.YearLevel
      AND NOT EXISTS (SELECT 1 FROM dbo.StudentModules sm WHERE sm.StudentId = u.UserId AND sm.ModuleId = @ModuleId);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_CourseModule_Unlink @CourseId INT, @ModuleId INT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRAN;
    DELETE dbo.CourseModules WHERE CourseId = @CourseId AND ModuleId = @ModuleId;
    DELETE sm FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId
    WHERE sm.ModuleId = @ModuleId AND u.CourseId = @CourseId;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Course_CreateWithModules
    @Code NVARCHAR(20), @Name NVARCHAR(150), @Description NVARCHAR(500), @Modules dbo.ModuleListType READONLY,
    @DepartmentId INT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @DepartmentId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @DepartmentId)
        THROW 50532, N'Choose the faculty and department this course falls under.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Courses WHERE Code = @Code) THROW 50530, N'A course with that code already exists.', 1;
    IF EXISTS (SELECT Code FROM @Modules GROUP BY Code HAVING COUNT(*) > 1) THROW 50531, N'Each module code can only appear once in the list.', 1;

    BEGIN TRAN;
    INSERT dbo.Courses (Code, Name, Description, DepartmentId) VALUES (UPPER(@Code), @Name, @Description, @DepartmentId);
    DECLARE @CourseId INT = SCOPE_IDENTITY();

    -- New codes become new modules; existing codes are simply linked.
    INSERT dbo.Modules (Code, Name, YearLevel, Semester, LecturerId)
    SELECT UPPER(n.Code), n.Name, n.YearLevel, n.Semester, n.LecturerId FROM @Modules n
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Modules m WHERE m.Code = n.Code);

    INSERT dbo.CourseModules (CourseId, ModuleId)
    SELECT DISTINCT @CourseId, m.ModuleId FROM dbo.Modules m JOIN @Modules n ON n.Code = m.Code;
    COMMIT;
    SELECT @CourseId AS CourseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Course_Delete @CourseId INT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE CourseId = @CourseId)
        THROW 50540, N'Students are registered for this course, so it cannot be deleted.', 1;
    DELETE dbo.CourseModules WHERE CourseId = @CourseId;
    UPDATE dbo.TutorApplications SET CourseId = NULL WHERE CourseId = @CourseId;
    DELETE dbo.Courses WHERE CourseId = @CourseId;
END
GO

/* ---------- Analytics & transactions ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Admin_Analytics AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @From DATE = DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(dbo.fn_SaNow()), MONTH(dbo.fn_SaNow()), 1));

    -- 0 registration trend
    SELECT FORMAT(DATEFROMPARTS(YEAR(CreatedAt), MONTH(CreatedAt), 1), 'MMM yyyy') AS Label, CAST(COUNT(*) AS DECIMAL(18,2)) AS Value
    FROM dbo.Users WHERE CreatedAt >= @From
    GROUP BY YEAR(CreatedAt), MONTH(CreatedAt) ORDER BY YEAR(CreatedAt), MONTH(CreatedAt);

    -- 1 role breakdown
    SELECT Role AS Label, CAST(COUNT(*) AS DECIMAL(18,2)) AS Value FROM dbo.Users WHERE IsActive = 1 GROUP BY Role ORDER BY Role;

    -- 2 application funnel
    SELECT s.Label, CAST(ISNULL(x.Cnt, 0) AS DECIMAL(18,2)) AS Value
    FROM (VALUES (1,'Submitted'),(2,'UnderReview'),(3,'Recommended'),(4,'NotRecommended'),(5,'Approved'),(6,'Rejected')) s(Ord, Label)
    LEFT JOIN (SELECT Status, COUNT(*) AS Cnt FROM dbo.TutorApplications GROUP BY Status) x ON x.Status = s.Label
    ORDER BY s.Ord;

    -- 3 student directory
    ;WITH Att AS (
        SELECT StudentId, Status FROM dbo.TutorialAttendance
        UNION ALL SELECT StudentId, Status FROM dbo.ClassRegisterAttendance
        UNION ALL SELECT StudentId, AttendanceStatus FROM dbo.Bookings WHERE AttendanceStatus IS NOT NULL
    )
    SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.StudentNumber, u.YearOfStudy, u.CreatedAt, c.Name AS CourseName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm WHERE sm.StudentId = u.UserId) AS ModuleCount,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.StudentId = u.UserId) AS BookingCount,
           CAST(ISNULL((SELECT 100.0 * SUM(CASE WHEN a.Status IN ('Present','Late') THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0)
                        FROM Att a WHERE a.StudentId = u.UserId), 0) AS DECIMAL(5,1)) AS AttendanceRate
    FROM dbo.Users u LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    WHERE u.Role = 'Student' AND u.IsActive = 1 ORDER BY u.LastName, u.FirstName;

    -- 4 students by course & year
    SELECT ISNULL(c.Name, N'No course') AS CourseName, u.YearOfStudy, COUNT(*) AS StudentCount
    FROM dbo.Users u LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    WHERE u.Role = 'Student' AND u.IsActive = 1
    GROUP BY c.Name, u.YearOfStudy ORDER BY CourseName, u.YearOfStudy;

    -- 5 attendance rate by module
    ;WITH A AS (
        SELECT tu.ModuleId, a.Status FROM dbo.TutorialAttendance a JOIN dbo.Tutorials tu ON tu.TutorialId = a.TutorialId
        UNION ALL SELECT cr.ModuleId, a.Status FROM dbo.ClassRegisterAttendance a JOIN dbo.ClassRegisters cr ON cr.RegisterId = a.RegisterId
        UNION ALL SELECT ModuleId, AttendanceStatus FROM dbo.Bookings WHERE AttendanceStatus IS NOT NULL
    )
    SELECT m.Code AS ModuleCode, m.Name AS ModuleName, COUNT(A.Status) AS Total,
           SUM(CASE WHEN A.Status IN ('Present','Late') THEN 1 ELSE 0 END) AS Present,
           CAST(CASE WHEN COUNT(A.Status) = 0 THEN 0 ELSE 100.0 * SUM(CASE WHEN A.Status IN ('Present','Late') THEN 1 ELSE 0 END) / COUNT(A.Status) END AS DECIMAL(5,1)) AS Rate
    FROM dbo.Modules m LEFT JOIN A ON A.ModuleId = m.ModuleId
    GROUP BY m.Code, m.Name ORDER BY m.Code;

    -- 6 modules with no assigned tutor
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, l.FirstName + ' ' + l.LastName AS LecturerName,
           (SELECT COUNT(*) FROM dbo.StudentModules sm WHERE sm.ModuleId = m.ModuleId) AS StudentCount
    FROM dbo.Modules m LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId)
    ORDER BY m.Code;

    -- 7 tutor coverage
    EXEC dbo.sp_Lecturer_Assignments @LecturerId = NULL;

    -- 8 tutor activity
    SELECT t.UserId AS TutorId, t.FirstName + ' ' + t.LastName AS TutorName,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.TutorId = t.UserId) AS Sessions,
           (SELECT ISNULL(SUM(b.Hours), 0) FROM dbo.Bookings b WHERE b.TutorId = t.UserId AND b.Status = 'Completed') AS Hours,
           (SELECT COUNT(*) FROM dbo.Tutorials tu WHERE tu.TutorId = t.UserId) AS Tutorials,
           (SELECT COUNT(*) FROM dbo.ClassRegisters cr WHERE cr.TutorId = t.UserId) AS Registers,
           (SELECT COUNT(*) FROM dbo.Announcements an WHERE an.TutorId = t.UserId) AS Announcements,
           (SELECT ISNULL(SUM(po.Amount), 0) FROM dbo.Payouts po WHERE po.TutorId = t.UserId AND po.Status = 'Sent') AS PaidOut
    FROM dbo.Users t WHERE t.Role = 'Tutor' AND t.IsActive = 1 ORDER BY TutorName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_Transactions AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PaymentId, p.PaymentRef, p.Hours, p.HourlyRate, p.Amount, p.Status, p.PfPaymentId, p.BookingId, p.CreatedAt, p.CompletedAt,
           p.PaymentMethod, p.PayerBank, p.PayerAccountLast4,
           s.FirstName + ' ' + s.LastName AS StudentName, t.FirstName + ' ' + t.LastName AS TutorName, m.Code AS ModuleCode,
           po.PayoutId, po.Status AS PayoutStatus, po.ProviderReference AS PayoutReference, po.Message AS PayoutMessage,
           po.ProcessedAt AS PayoutProcessedAt, bd.AccountLast4
    FROM dbo.Payments p JOIN dbo.Users s ON s.UserId = p.StudentId JOIN dbo.Users t ON t.UserId = p.TutorId
    JOIN dbo.Modules m ON m.ModuleId = p.ModuleId
    LEFT JOIN dbo.Payouts po ON po.PaymentId = p.PaymentId LEFT JOIN dbo.BankingDetails bd ON bd.TutorId = p.TutorId
    ORDER BY p.CreatedAt DESC;

    SELECT
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payments WHERE Status = 'Complete') AS Collected,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payouts WHERE Status = 'Sent') AS PaidOut,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payouts WHERE Status <> 'Sent') AS Outstanding,
        (SELECT COUNT(*) FROM dbo.Payments WHERE Status = 'Pending') AS PendingCheckouts;
END
GO
/* ---------- Notification centre (top-bar bell) ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Notifications_Get @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Seen DATETIME2 = (SELECT NotificationsSeenAt FROM dbo.Users WHERE UserId = @UserId);
    SELECT TOP 12 ActivityId, Message, Link, CreatedAt,
           CAST(CASE WHEN @Seen IS NULL OR CreatedAt > @Seen THEN 1 ELSE 0 END AS BIT) AS IsUnread
    FROM dbo.ActivityLog WHERE UserId = @UserId ORDER BY CreatedAt DESC, ActivityId DESC;
    SELECT COUNT(*) AS Unread FROM dbo.ActivityLog
    WHERE UserId = @UserId AND (@Seen IS NULL OR CreatedAt > @Seen);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Notifications_MarkSeen @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET NotificationsSeenAt = dbo.fn_SaNow() WHERE UserId = @UserId;
END
GO
/* ---------- Profile (Settings page) ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_UpdateProfile
    @UserId INT, @Phone NVARCHAR(20), @Campus NVARCHAR(80), @Gender NVARCHAR(30),
    @EmergencyContactName NVARCHAR(120), @EmergencyContactPhone NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET Phone = @Phone, Campus = @Campus, Gender = @Gender,
        EmergencyContactName = @EmergencyContactName, EmergencyContactPhone = @EmergencyContactPhone
    WHERE UserId = @UserId;
    INSERT dbo.ActivityLog (UserId, Message) VALUES (@UserId, N'You updated your profile details.');
END
GO

/* ---------- Sign-in protection: lock the account for 15 minutes after 5 failed attempts ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_LoginFailed @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET FailedLoginCount = FailedLoginCount + 1,
        LockoutUntil = CASE WHEN FailedLoginCount + 1 >= 5 THEN DATEADD(MINUTE, 15, dbo.fn_SaNow()) ELSE LockoutUntil END
    WHERE UserId = @UserId;
    SELECT FailedLoginCount, LockoutUntil FROM dbo.Users WHERE UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_LoginSucceeded @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET FailedLoginCount = 0, LockoutUntil = NULL WHERE UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_Unlock @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET FailedLoginCount = 0, LockoutUntil = NULL WHERE UserId = @UserId;
END
GO

/* ---------- POPIA: right of access (download my data) and deletion requests ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_DataExport @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Email NVARCHAR(150) = (SELECT Email FROM dbo.Users WHERE UserId = @UserId);
    -- 0 profile (never the password hash)
    SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.StudentNumber, u.Phone, c.Name AS CourseName, u.YearOfStudy,
           u.PersonalEmail, u.DateOfBirth, u.Gender, u.Campus, u.EmergencyContactName, u.EmergencyContactPhone,
           u.DarkMode, u.EmailNotifications, u.CreatedAt, u.PopiaConsentAt, u.DeletionRequestedAt
    FROM dbo.Users u LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId WHERE u.UserId = @UserId;
    -- 1 registered modules
    SELECT m.Code, m.Name, m.YearLevel FROM dbo.StudentModules sm JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId WHERE sm.StudentId = @UserId;
    -- 2 bookings (as student or tutor)
    SELECT b.BookingId, m.Code AS ModuleCode, b.StartTime, b.EndTime, b.Hours, b.Amount, b.Status, b.AttendanceStatus,
           CASE WHEN b.StudentId = @UserId THEN 'Student' ELSE 'Tutor' END AS YourRole
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId WHERE b.StudentId = @UserId OR b.TutorId = @UserId;
    -- 3 payments
    SELECT p.PaymentRef, p.Amount, p.Status, p.CreatedAt, p.CompletedAt FROM dbo.Payments p WHERE p.StudentId = @UserId;
    -- 4 attendance
    SELECT 'Tutorial' AS Source, tu.Title, tu.StartTime AS [Date], a.Status, a.Method
    FROM dbo.TutorialAttendance a JOIN dbo.Tutorials tu ON tu.TutorialId = a.TutorialId WHERE a.StudentId = @UserId
    UNION ALL
    SELECT 'Class register', cr.Title, CAST(cr.RegisterDate AS DATETIME2), a.Status, 'Manual'
    FROM dbo.ClassRegisterAttendance a JOIN dbo.ClassRegisters cr ON cr.RegisterId = a.RegisterId WHERE a.StudentId = @UserId;
    -- 5 tutor applications
    SELECT a.ApplicationId, m.Code AS ModuleCode, a.TutorYear, a.ModuleMark, a.Status, a.SubmittedAt, a.DecidedAt
    FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId WHERE a.StudentId = @UserId OR a.Email = @Email;
    -- 6 banking (masked)
    SELECT BankName, AccountHolder, AccountType, BranchCode, N'****' + AccountLast4 AS AccountNumber, UpdatedAt
    FROM dbo.BankingDetails WHERE TutorId = @UserId;
    -- 7 activity
    SELECT Message, CreatedAt FROM dbo.ActivityLog WHERE UserId = @UserId ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_RequestDeletion @UserId INT, @Cancel BIT = 0 AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET DeletionRequestedAt = CASE WHEN @Cancel = 1 THEN NULL ELSE dbo.fn_SaNow() END WHERE UserId = @UserId;
    INSERT dbo.ActivityLog (UserId, Message)
    VALUES (@UserId, CASE WHEN @Cancel = 1 THEN N'You cancelled your account deletion request.'
                          ELSE N'You asked for your account and personal information to be deleted. An administrator will process it.' END);
END
GO

/* ---------- REST API personal access tokens ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_ApiToken_Create @UserId INT, @TokenHash NVARCHAR(64), @Name NVARCHAR(60), @ExpiresAt DATETIME2 AS
BEGIN
    SET NOCOUNT ON;
    IF (SELECT COUNT(*) FROM dbo.ApiTokens WHERE UserId = @UserId AND RevokedAt IS NULL AND ExpiresAt > dbo.fn_SaNow()) >= 10
        THROW 50601, N'You already have 10 active API tokens. Revoke one first.', 1;
    INSERT dbo.ApiTokens (UserId, TokenHash, Name, ExpiresAt) VALUES (@UserId, @TokenHash, @Name, @ExpiresAt);
    SELECT SCOPE_IDENTITY() AS TokenId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ApiToken_Validate @TokenHash NVARCHAR(64) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TokenId INT, @UserId INT;
    SELECT @TokenId = TokenId, @UserId = UserId FROM dbo.ApiTokens
    WHERE TokenHash = @TokenHash AND RevokedAt IS NULL AND ExpiresAt > dbo.fn_SaNow();
    IF @TokenId IS NULL RETURN;
    UPDATE dbo.ApiTokens SET LastUsedAt = dbo.fn_SaNow() WHERE TokenId = @TokenId;
    SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.YearOfStudy, u.IsActive
    FROM dbo.Users u WHERE u.UserId = @UserId AND u.IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ApiToken_List @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT TokenId, Name, CreatedAt, ExpiresAt, LastUsedAt, RevokedAt
    FROM dbo.ApiTokens WHERE UserId = @UserId ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ApiToken_Revoke @UserId INT, @TokenId INT = NULL, @TokenHash NVARCHAR(64) = NULL AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ApiTokens SET RevokedAt = dbo.fn_SaNow()
    WHERE UserId = @UserId AND RevokedAt IS NULL
      AND (@TokenId IS NULL OR TokenId = @TokenId)
      AND (@TokenHash IS NULL OR TokenHash = @TokenHash);
    SELECT @@ROWCOUNT AS Affected;
END
GO

/* ---------- API: tutor's tutorials for the student view, lightweight module list ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Api_Modules AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, l.FirstName + ' ' + l.LastName AS LecturerName,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId) AS TutorCount
    FROM dbo.Modules m LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId
    ORDER BY m.YearLevel, m.Code;
END
GO

/* ---------- POPIA: record consent for users created before consent was collected ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_RecordConsent @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users SET PopiaConsentAt = dbo.fn_SaNow() WHERE UserId = @UserId AND PopiaConsentAt IS NULL;
END
GO

/* ---------- Faculties and departments ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Faculty_List AS
BEGIN
    SET NOCOUNT ON;
    SELECT f.FacultyId, f.Name,
           (SELECT COUNT(*) FROM dbo.Departments d WHERE d.FacultyId = f.FacultyId) AS DepartmentCount,
           (SELECT COUNT(*) FROM dbo.Courses c JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId WHERE d.FacultyId = f.FacultyId) AS CourseCount
    FROM dbo.Faculties f ORDER BY f.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Department_List @FacultyId INT = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.DepartmentId, d.FacultyId, f.Name AS FacultyName, d.Name,
           (SELECT COUNT(*) FROM dbo.Courses c WHERE c.DepartmentId = d.DepartmentId) AS CourseCount
    FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    WHERE @FacultyId IS NULL OR d.FacultyId = @FacultyId
    ORDER BY f.Name, d.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Faculty_Save @FacultyId INT = NULL, @Name NVARCHAR(120) AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Faculties WHERE Name = @Name AND (@FacultyId IS NULL OR FacultyId <> @FacultyId))
        THROW 50550, N'A faculty with that name already exists.', 1;
    IF @FacultyId IS NULL
    BEGIN
        INSERT dbo.Faculties (Name) VALUES (@Name);
        SELECT SCOPE_IDENTITY() AS FacultyId;
    END
    ELSE
    BEGIN
        UPDATE dbo.Faculties SET Name = @Name WHERE FacultyId = @FacultyId;
        SELECT @FacultyId AS FacultyId;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Faculty_Delete @FacultyId INT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Departments WHERE FacultyId = @FacultyId)
        THROW 50551, N'Remove or move this faculty''s departments first.', 1;
    DELETE dbo.Faculties WHERE FacultyId = @FacultyId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Department_Save @DepartmentId INT = NULL, @FacultyId INT, @Name NVARCHAR(120) AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Faculties WHERE FacultyId = @FacultyId)
        THROW 50552, N'Choose the faculty this department belongs to.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Departments WHERE FacultyId = @FacultyId AND Name = @Name AND (@DepartmentId IS NULL OR DepartmentId <> @DepartmentId))
        THROW 50553, N'That faculty already has a department with this name.', 1;
    IF @DepartmentId IS NULL
    BEGIN
        INSERT dbo.Departments (FacultyId, Name) VALUES (@FacultyId, @Name);
        SELECT SCOPE_IDENTITY() AS DepartmentId;
    END
    ELSE
    BEGIN
        UPDATE dbo.Departments SET FacultyId = @FacultyId, Name = @Name WHERE DepartmentId = @DepartmentId;
        SELECT @DepartmentId AS DepartmentId;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Department_Delete @DepartmentId INT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Courses WHERE DepartmentId = @DepartmentId)
        THROW 50554, N'Move this department''s courses to another department first.', 1;
    DELETE dbo.Departments WHERE DepartmentId = @DepartmentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Course_SetDepartment @CourseId INT, @DepartmentId INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @DepartmentId)
        THROW 50555, N'Choose a department.', 1;
    UPDATE dbo.Courses SET DepartmentId = @DepartmentId WHERE CourseId = @CourseId;
END
GO

/* ---------- AI study assistant: usage log for rate limiting (no question or answer text is stored) ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Ai_RecentCount @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS RequestsLastHour FROM dbo.AiRequests
    WHERE UserId = @UserId AND CreatedAt > DATEADD(HOUR, -1, dbo.fn_SaNow());
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Ai_LogRequest @UserId INT, @Mode NVARCHAR(30), @ModuleCode NVARCHAR(20) = NULL AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.AiRequests (UserId, Mode, ModuleCode) VALUES (@UserId, @Mode, @ModuleCode);
END
GO


/* ---------- Lecturer: how many tutors each module needs ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Module_SetTutorsNeeded @ModuleId INT, @LecturerId INT, @Needed INT AS
BEGIN
    SET NOCOUNT ON;
    IF @Needed NOT BETWEEN 0 AND 50 THROW 50306, N'Choose between 0 and 50 tutors.', 1;
    UPDATE dbo.Modules SET TutorsNeeded = @Needed WHERE ModuleId = @ModuleId AND LecturerId = @LecturerId;
    IF @@ROWCOUNT = 0 THROW 50307, N'You can only change modules you lecture.', 1;
END
GO

/* Tutors a lecturer can choose from, with the modules each tutors and the modules they were approved for. */
CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_TutorPool @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.UserId, t.FirstName, t.LastName, t.Email, t.StudentNumber,
           STUFF((SELECT N',' + m.Code FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
                  WHERE tm.TutorId = t.UserId FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS TutorsModules,
           STUFF((SELECT N',' + m.Code FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId
                  WHERE a.Email = t.Email AND a.Status = 'Approved' FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS ApprovedModules,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s WHERE s.TutorId = t.UserId AND s.IsBooked = 0 AND s.StartTime > dbo.fn_SaNow()) AS OpenSlots
    FROM dbo.Users t WHERE t.Role = 'Tutor' AND t.IsActive = 1
    ORDER BY t.FirstName, t.LastName;
END
GO

/* ---------- Check in by typing the tutorial's code (same rules as scanning) ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_CheckInByCode @Code NVARCHAR(8), @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Token UNIQUEIDENTIFIER = (SELECT QrToken FROM dbo.Tutorials WHERE CheckInCode = UPPER(LTRIM(RTRIM(@Code))));
    IF @Token IS NULL BEGIN SELECT 'NotFound' AS Result, CAST(NULL AS NVARCHAR(10)) AS Status, CAST(NULL AS INT) AS TutorialId; RETURN; END
    EXEC dbo.sp_Tutorial_QrCheckIn @Token = @Token, @StudentId = @StudentId;
END
GO

/* ---------- Admin <-> lecturer notes on a tutor application ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_ApplicationComment_Add @ApplicationId INT, @UserId INT, @Role NVARCHAR(20), @Comment NVARCHAR(1000) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Lecturer INT, @Name NVARCHAR(130);
    SELECT @Lecturer = AssignedLecturerId, @Name = FirstName + N' ' + LastName FROM dbo.TutorApplications WHERE ApplicationId = @ApplicationId;
    IF @Name IS NULL THROW 50420, N'That application no longer exists.', 1;
    IF @Role = 'Lecturer' AND ISNULL(@Lecturer, 0) <> @UserId THROW 50421, N'Only the lecturer reviewing this application can add notes.', 1;
    IF @Role NOT IN ('Lecturer','Admin') THROW 50422, N'Only administrators and the reviewing lecturer can add notes.', 1;
    INSERT dbo.ApplicationComments (ApplicationId, UserId, Role, Comment) VALUES (@ApplicationId, @UserId, @Role, @Comment);
    -- Let the other side know.
    IF @Role = 'Admin' AND @Lecturer IS NOT NULL
        INSERT dbo.ActivityLog (UserId, Message) VALUES (@Lecturer, N'An administrator added a note on the application from ' + @Name + N'.');
    IF @Role = 'Lecturer'
        INSERT dbo.ActivityLog (UserId, Message)
        SELECT UserId, N'The reviewing lecturer added a note on the application from ' + @Name + N'.' FROM dbo.Users WHERE Role = 'Admin' AND IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ApplicationComment_List @ApplicationId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CommentId, c.Role, c.Comment, c.CreatedAt, u.FirstName + ' ' + u.LastName AS AuthorName
    FROM dbo.ApplicationComments c JOIN dbo.Users u ON u.UserId = c.UserId
    WHERE c.ApplicationId = @ApplicationId ORDER BY c.CreatedAt;
END
GO

/* ---------- Admin: everything about one user ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_Details @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    -- 0: profile with course, department and faculty
    SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.StudentNumber, u.Phone, u.PersonalEmail, u.YearOfStudy,
           u.DateOfBirth, u.Gender, u.Campus, u.EmergencyContactName, u.EmergencyContactPhone, u.IsActive, u.CreatedAt,
           u.DarkMode, u.EmailNotifications, u.PopiaConsentAt, u.DeletionRequestedAt, u.FailedLoginCount, u.LockoutUntil,
           c.Code AS CourseCode, c.Name AS CourseName, d.Name AS DepartmentName, f.Name AS FacultyName
    FROM dbo.Users u
    LEFT JOIN dbo.Courses c ON c.CourseId = u.CourseId
    LEFT JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
    LEFT JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    WHERE u.UserId = @UserId;
    -- 1: modules (registered as a student, tutored, or lectured)
    SELECT m.Code, m.Name, m.YearLevel, N'Registered' AS Relation FROM dbo.StudentModules sm JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId WHERE sm.StudentId = @UserId
    UNION ALL
    SELECT m.Code, m.Name, tm.YearOfStudy, N'Tutors' FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId WHERE tm.TutorId = @UserId
    UNION ALL
    SELECT m.Code, m.Name, m.YearLevel, N'Lectures' FROM dbo.Modules m WHERE m.LecturerId = @UserId
    ORDER BY 4, 1;
    -- 2: bookings as student or tutor
    SELECT b.BookingId, m.Code AS ModuleCode, b.StartTime, b.EndTime, b.Hours, b.Amount, b.Status, b.AttendanceStatus,
           s.FirstName + ' ' + s.LastName AS StudentName, t.FirstName + ' ' + t.LastName AS TutorName
    FROM dbo.Bookings b JOIN dbo.Modules m ON m.ModuleId = b.ModuleId
    JOIN dbo.Users s ON s.UserId = b.StudentId JOIN dbo.Users t ON t.UserId = b.TutorId
    WHERE b.StudentId = @UserId OR b.TutorId = @UserId ORDER BY b.StartTime DESC;
    -- 3: payments made or received, with payout status
    SELECT p.PaymentRef, p.Amount, p.Status, p.CreatedAt, p.CompletedAt, po.Status AS PayoutStatus,
           CASE WHEN p.StudentId = @UserId THEN N'Paid' ELSE N'Received' END AS Direction
    FROM dbo.Payments p LEFT JOIN dbo.Payouts po ON po.PaymentId = p.PaymentId
    WHERE p.StudentId = @UserId OR p.TutorId = @UserId ORDER BY p.CreatedAt DESC;
    -- 4: attendance totals
    SELECT
        (SELECT COUNT(*) FROM dbo.TutorialAttendance WHERE StudentId = @UserId AND Status = 'Present')
      + (SELECT COUNT(*) FROM dbo.ClassRegisterAttendance WHERE StudentId = @UserId AND Status = 'Present')
      + (SELECT COUNT(*) FROM dbo.Bookings WHERE StudentId = @UserId AND AttendanceStatus = 'Present') AS Present,
        (SELECT COUNT(*) FROM dbo.TutorialAttendance WHERE StudentId = @UserId AND Status = 'Absent')
      + (SELECT COUNT(*) FROM dbo.ClassRegisterAttendance WHERE StudentId = @UserId AND Status = 'Absent')
      + (SELECT COUNT(*) FROM dbo.Bookings WHERE StudentId = @UserId AND AttendanceStatus = 'Absent') AS Absent,
        (SELECT COUNT(*) FROM dbo.Tutorials WHERE TutorId = @UserId) AS TutorialsRun,
        (SELECT COUNT(*) FROM dbo.AvailabilitySlots WHERE TutorId = @UserId AND IsBooked = 0 AND StartTime > dbo.fn_SaNow()) AS OpenSlots;
    -- 5: tutor applications
    SELECT a.ApplicationId, m.Code AS ModuleCode, a.TutorYear, a.ModuleMark, a.Status, a.SubmittedAt, a.DecidedAt
    FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId JOIN dbo.Users u ON u.UserId = @UserId
    WHERE a.StudentId = @UserId OR a.Email = u.Email ORDER BY a.SubmittedAt DESC;
    -- 6: banking (masked)
    SELECT BankName, AccountHolder, AccountType, BranchCode, AccountLast4, UpdatedAt FROM dbo.BankingDetails WHERE TutorId = @UserId;
    -- 7: recent activity
    SELECT TOP 25 Message, CreatedAt FROM dbo.ActivityLog WHERE UserId = @UserId ORDER BY CreatedAt DESC;
END
GO

/* ---------- Admin analytics: the detailed view ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Admin_AnalyticsDetail AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2 = dbo.fn_SaNow();
    -- 0: faculties
    SELECT f.FacultyId, f.Name,
           (SELECT COUNT(*) FROM dbo.Departments d WHERE d.FacultyId = f.FacultyId) AS Departments,
           (SELECT COUNT(*) FROM dbo.Courses c JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId WHERE d.FacultyId = f.FacultyId) AS Courses,
           (SELECT COUNT(DISTINCT cm.ModuleId) FROM dbo.CourseModules cm JOIN dbo.Courses c ON c.CourseId = cm.CourseId JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId WHERE d.FacultyId = f.FacultyId) AS Modules,
           (SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Courses c ON c.CourseId = u.CourseId JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId
             WHERE d.FacultyId = f.FacultyId AND u.Role = 'Student' AND u.IsActive = 1) AS Students
    FROM dbo.Faculties f ORDER BY f.Name;
    -- 1: departments
    SELECT f.Name AS FacultyName, d.Name AS DepartmentName,
           (SELECT COUNT(*) FROM dbo.Courses c WHERE c.DepartmentId = d.DepartmentId) AS Courses,
           (SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Courses c ON c.CourseId = u.CourseId
             WHERE c.DepartmentId = d.DepartmentId AND u.Role = 'Student' AND u.IsActive = 1) AS Students
    FROM dbo.Departments d JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId ORDER BY f.Name, d.Name;
    -- 2: courses with students per year
    SELECT c.Code, c.Name, d.Name AS DepartmentName, f.Name AS FacultyName,
           (SELECT COUNT(*) FROM dbo.CourseModules cm WHERE cm.CourseId = c.CourseId) AS Modules,
           SUM(CASE WHEN u.YearOfStudy = 1 THEN 1 ELSE 0 END) AS Year1,
           SUM(CASE WHEN u.YearOfStudy = 2 THEN 1 ELSE 0 END) AS Year2,
           SUM(CASE WHEN u.YearOfStudy = 3 THEN 1 ELSE 0 END) AS Year3,
           SUM(CASE WHEN u.YearOfStudy >= 4 THEN 1 ELSE 0 END) AS Year4Plus,
           COUNT(u.UserId) AS Students
    FROM dbo.Courses c
    LEFT JOIN dbo.Departments d ON d.DepartmentId = c.DepartmentId LEFT JOIN dbo.Faculties f ON f.FacultyId = d.FacultyId
    LEFT JOIN dbo.Users u ON u.CourseId = c.CourseId AND u.Role = 'Student' AND u.IsActive = 1
    GROUP BY c.CourseId, c.Code, c.Name, d.Name, f.Name ORDER BY f.Name, d.Name, c.Name;
    -- 3: bookings by status
    SELECT Status AS Label, COUNT(*) AS Value, ISNULL(SUM(Amount), 0) AS Amount FROM dbo.Bookings GROUP BY Status ORDER BY COUNT(*) DESC;
    -- 4: money per month (last 12 months)
    SELECT FORMAT(DATEFROMPARTS(YEAR(p.CompletedAt), MONTH(p.CompletedAt), 1), 'MMM yyyy') AS Label,
           MIN(DATEFROMPARTS(YEAR(p.CompletedAt), MONTH(p.CompletedAt), 1)) AS MonthStart,
           COUNT(*) AS Payments, SUM(p.Amount) AS Collected,
           ISNULL(SUM(CASE WHEN po.Status = 'Sent' THEN po.Amount END), 0) AS PaidOut
    FROM dbo.Payments p LEFT JOIN dbo.Payouts po ON po.PaymentId = p.PaymentId
    WHERE p.Status = 'Complete' AND p.CompletedAt >= DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1))
    GROUP BY YEAR(p.CompletedAt), MONTH(p.CompletedAt) ORDER BY MonthStart;
    -- 5: tutor supply and demand per module
    SELECT m.Code, m.Name, m.YearLevel, l.FirstName + ' ' + l.LastName AS LecturerName, m.TutorsNeeded,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId) AS TutorCount,
           (SELECT COUNT(*) FROM dbo.StudentModules sm WHERE sm.ModuleId = m.ModuleId) AS Students,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s WHERE s.IsBooked = 0 AND s.StartTime > @Now
              AND s.TutorId IN (SELECT tm.TutorId FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId)) AS OpenSlots,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.ModuleId = m.ModuleId) AS Bookings,
           (SELECT COUNT(*) FROM dbo.Tutorials tu WHERE tu.ModuleId = m.ModuleId) AS Tutorials
    FROM dbo.Modules m LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId ORDER BY m.YearLevel, m.Code;
    -- 6: lecturer workload
    SELECT l.UserId, l.FirstName + ' ' + l.LastName AS LecturerName,
           (SELECT COUNT(*) FROM dbo.Modules m WHERE m.LecturerId = l.UserId) AS Modules,
           (SELECT ISNULL(SUM(m.TutorsNeeded), 0) FROM dbo.Modules m WHERE m.LecturerId = l.UserId) AS TutorsNeeded,
           (SELECT COUNT(*) FROM dbo.TutorModules tm JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId WHERE m.LecturerId = l.UserId) AS TutorsAssigned,
           (SELECT COUNT(DISTINCT sm.StudentId) FROM dbo.StudentModules sm JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId WHERE m.LecturerId = l.UserId) AS Students,
           (SELECT COUNT(*) FROM dbo.CourseContent c JOIN dbo.Modules m ON m.ModuleId = c.ModuleId WHERE m.LecturerId = l.UserId) AS ContentItems,
           (SELECT COUNT(*) FROM dbo.TutorApplications a WHERE a.AssignedLecturerId = l.UserId AND a.Status = 'UnderReview') AS ApplicationsToReview
    FROM dbo.Users l WHERE l.Role = 'Lecturer' AND l.IsActive = 1 ORDER BY LecturerName;
    -- 7: tutors ranked by activity and earnings
    SELECT t.FirstName + ' ' + t.LastName AS TutorName,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.TutorId = t.UserId) AS Modules,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.TutorId = t.UserId AND b.Status IN ('Confirmed','Completed')) AS Sessions,
           (SELECT ISNULL(SUM(b.Hours), 0) FROM dbo.Bookings b WHERE b.TutorId = t.UserId AND b.Status = 'Completed') AS Hours,
           (SELECT COUNT(*) FROM dbo.Tutorials tu WHERE tu.TutorId = t.UserId) AS Tutorials,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots s WHERE s.TutorId = t.UserId AND s.IsBooked = 0 AND s.StartTime > @Now) AS OpenSlots,
           (SELECT ISNULL(SUM(po.Amount), 0) FROM dbo.Payouts po JOIN dbo.Payments p ON p.PaymentId = po.PaymentId WHERE p.TutorId = t.UserId AND po.Status = 'Sent') AS Earned
    FROM dbo.Users t WHERE t.Role = 'Tutor' AND t.IsActive = 1 ORDER BY Sessions DESC, Hours DESC;
    -- 8: tutorials and how students checked in
    SELECT m.Code, COUNT(DISTINCT tu.TutorialId) AS Tutorials,
           SUM(CASE WHEN a.Method = 'QR' AND a.Status = 'Present' THEN 1 ELSE 0 END) AS QrCheckIns,
           SUM(CASE WHEN a.Method <> 'QR' AND a.Status = 'Present' THEN 1 ELSE 0 END) AS ManualPresent,
           SUM(CASE WHEN a.Status = 'Absent' THEN 1 ELSE 0 END) AS Absent
    FROM dbo.Tutorials tu JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId
    LEFT JOIN dbo.TutorialAttendance a ON a.TutorialId = tu.TutorialId
    GROUP BY m.Code ORDER BY m.Code;
    -- 9: tutor applications per module
    SELECT m.Code, COUNT(*) AS Total,
           SUM(CASE WHEN a.Status IN ('Submitted','UnderReview') THEN 1 ELSE 0 END) AS OpenApplications,
           SUM(CASE WHEN a.Status IN ('Recommended','NotRecommended') THEN 1 ELSE 0 END) AS Recommended,
           SUM(CASE WHEN a.Status = 'Approved' THEN 1 ELSE 0 END) AS Approved,
           SUM(CASE WHEN a.Status = 'Rejected' THEN 1 ELSE 0 END) AS Rejected,
           AVG(CAST(a.ModuleMark AS DECIMAL(6,2))) AS AverageMark
    FROM dbo.TutorApplications a JOIN dbo.Modules m ON m.ModuleId = a.ModuleId GROUP BY m.Code ORDER BY m.Code;
    -- 10: AI assistant use per module and mode
    SELECT ISNULL(r.ModuleCode, N'-') AS Code, r.Mode, COUNT(*) AS Requests, MAX(r.CreatedAt) AS LastUsed
    FROM dbo.AiRequests r GROUP BY r.ModuleCode, r.Mode ORDER BY COUNT(*) DESC;
    -- 11: payment and payout status counts
    SELECT N'Payment' AS Kind, Status AS Label, COUNT(*) AS Value, ISNULL(SUM(Amount), 0) AS Amount FROM dbo.Payments GROUP BY Status
    UNION ALL
    SELECT N'Payout', Status, COUNT(*), ISNULL(SUM(Amount), 0) FROM dbo.Payouts GROUP BY Status;
    -- 12: platform totals
    SELECT
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Student' AND IsActive = 1) AS Students,
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Tutor' AND IsActive = 1) AS Tutors,
        (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'Lecturer' AND IsActive = 1) AS Lecturers,
        (SELECT COUNT(*) FROM dbo.Faculties) AS Faculties, (SELECT COUNT(*) FROM dbo.Departments) AS Departments,
        (SELECT COUNT(*) FROM dbo.Courses) AS Courses, (SELECT COUNT(*) FROM dbo.Modules) AS Modules,
        (SELECT COUNT(*) FROM dbo.Bookings) AS Bookings, (SELECT COUNT(*) FROM dbo.Tutorials) AS Tutorials,
        (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Payments WHERE Status = 'Complete') AS Revenue,
        (SELECT ISNULL(SUM(TutorsNeeded), 0) FROM dbo.Modules) AS TutorsNeeded,
        (SELECT COUNT(*) FROM dbo.TutorModules) AS TutorPlacements,
        (SELECT COUNT(*) FROM dbo.AvailabilitySlots WHERE IsBooked = 0 AND StartTime > @Now) AS OpenSlots,
        (SELECT COUNT(*) FROM dbo.AiRequests) AS AiRequests;
END
GO

/* ---------- Modules: add as many as needed in one go ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Module_BulkCreate @Modules dbo.ModuleListType READONLY AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM @Modules) THROW 50522, N'Add at least one module.', 1;
    IF EXISTS (SELECT 1 FROM @Modules WHERE YearLevel NOT BETWEEN 1 AND 7) THROW 50523, N'Year levels must be between 1 and 7.', 1;
    IF EXISTS (SELECT 1 FROM @Modules WHERE Semester NOT IN (1, 2)) THROW 50521, N'Choose semester 1 or semester 2 for every module.', 1;
    IF EXISTS (SELECT UPPER(Code) FROM @Modules GROUP BY UPPER(Code) HAVING COUNT(*) > 1)
        THROW 50524, N'The same module code appears more than once in your list.', 1;
    DECLARE @Taken NVARCHAR(400) = STUFF((SELECT N', ' + m.Code FROM dbo.Modules m JOIN @Modules n ON UPPER(n.Code) = m.Code
                                           FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(400)'), 1, 2, N'');
    IF @Taken IS NOT NULL
    BEGIN
        DECLARE @Msg NVARCHAR(500) = N'These module codes already exist: ' + @Taken + N'. Change or remove them.';
        THROW 50520, @Msg, 1;
    END
    INSERT dbo.Modules (Code, Name, YearLevel, Semester, LecturerId)
    SELECT UPPER(Code), Name, YearLevel, Semester, LecturerId FROM @Modules;
    SELECT @@ROWCOUNT AS Created;
END
GO

/* ---------- Bank payment (EFT checkout): record who paid from which bank (never the full account number) ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Payment_SetPayer
    @PaymentRef NVARCHAR(40), @StudentId INT, @Method NVARCHAR(20), @Bank NVARCHAR(60), @Last4 NVARCHAR(4)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Payments SET PaymentMethod = @Method, PayerBank = @Bank, PayerAccountLast4 = @Last4
    WHERE PaymentRef = @PaymentRef AND StudentId = @StudentId AND Status = 'Pending';
    SELECT @@ROWCOUNT AS Affected;
END
GO

/* ---------- A student tried to pay a tutor who has no banking details yet ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutor_BankingNeeded @TutorId INT, @StudentName NVARCHAR(130) AS
BEGIN
    SET NOCOUNT ON;
    -- one reminder per tutor per day is enough
    IF NOT EXISTS (SELECT 1 FROM dbo.ActivityLog WHERE UserId = @TutorId AND Message LIKE N'Add your banking details%'
                   AND CreatedAt > DATEADD(DAY, -1, dbo.fn_SaNow()))
        INSERT dbo.ActivityLog (UserId, Message)
        VALUES (@TutorId, N'Add your banking details: ' + @StudentName + N' tried to book and pay for a session, but payments to you can''t go through until your bank account is added.');
    SELECT u.Email, u.FirstName, u.EmailNotifications FROM dbo.Users u WHERE u.UserId = @TutorId;
END
GO

/* ---------- Tutor edits a tutorial ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutorial_Update
    @TutorialId INT, @TutorId INT, @ModuleId INT, @YearOfStudy INT, @Title NVARCHAR(150), @Description NVARCHAR(1000),
    @Venue NVARCHAR(150), @StartTime DATETIME2, @EndTime DATETIME2
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @OldModule INT, @OldYear INT;
    SELECT @OldModule = ModuleId, @OldYear = YearOfStudy FROM dbo.Tutorials WHERE TutorialId = @TutorialId AND TutorId = @TutorId;
    IF @OldModule IS NULL THROW 50222, N'That tutorial doesn''t exist or isn''t yours.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.TutorModules WHERE TutorId = @TutorId AND ModuleId = @ModuleId AND YearOfStudy = @YearOfStudy)
        THROW 50220, N'You are not assigned to that module for that year of study.', 1;
    IF @EndTime <= @StartTime THROW 50221, N'The end time must be after the start time.', 1;
    IF (@ModuleId <> @OldModule OR @YearOfStudy <> @OldYear) AND EXISTS (SELECT 1 FROM dbo.TutorialAttendance WHERE TutorialId = @TutorialId AND Method <> 'Auto')
        THROW 50223, N'Attendance has already been taken, so the module and year can''t change. Create a new tutorial instead.', 1;

    BEGIN TRAN;
    UPDATE dbo.Tutorials
    SET ModuleId = @ModuleId, YearOfStudy = @YearOfStudy, Title = @Title, Description = @Description,
        Venue = @Venue, StartTime = @StartTime, EndTime = @EndTime
    WHERE TutorialId = @TutorialId AND TutorId = @TutorId;
    -- moved to a time that hasn't finished yet: the automatic "didn't check in" absences no longer apply
    IF @EndTime > dbo.fn_SaNow()
        DELETE dbo.TutorialAttendance WHERE TutorialId = @TutorialId AND Method = 'Auto';

    INSERT dbo.ActivityLog (UserId, Message)
    SELECT u.UserId, N'Tutorial updated: ' + @Title + N' (' + m.Code + N') is now on ' + FORMAT(@StartTime, 'ddd dd MMM HH:mm')
                     + ISNULL(N' in ' + NULLIF(@Venue, N''), N'') + N'.'
    FROM dbo.StudentModules sm JOIN dbo.Users u ON u.UserId = sm.StudentId JOIN dbo.Modules m ON m.ModuleId = sm.ModuleId
    WHERE sm.ModuleId = @ModuleId AND u.YearOfStudy = @YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1;
    COMMIT;
END
GO

/* ---------- Notifications: full history and one item ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Notifications_List @UserId INT, @Take INT = 200 AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Seen DATETIME2 = (SELECT NotificationsSeenAt FROM dbo.Users WHERE UserId = @UserId);
    SELECT TOP (@Take) ActivityId, Message, Link, CreatedAt,
           CAST(CASE WHEN @Seen IS NULL OR CreatedAt > @Seen THEN 1 ELSE 0 END AS BIT) AS IsUnread
    FROM dbo.ActivityLog WHERE UserId = @UserId ORDER BY CreatedAt DESC, ActivityId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Notification_Get @ActivityId INT, @UserId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT ActivityId, Message, Link, CreatedAt, CAST(0 AS BIT) AS IsUnread
    FROM dbo.ActivityLog WHERE ActivityId = @ActivityId AND UserId = @UserId;
END
GO

/* ---------- Course content a student can see: lecturer material, and tutor material the lecturer approved ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Content_ForStudent @StudentId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ContentId, c.ModuleId, c.YearOfStudy, c.Title, c.Description, c.FileName, c.UploaderRole, c.UploadedAt,
           c.IsReviewed, m.Code AS ModuleCode, m.Name AS ModuleName, u.FirstName + ' ' + u.LastName AS UploaderName
    FROM dbo.CourseContent c
    JOIN dbo.Modules m ON m.ModuleId = c.ModuleId
    JOIN dbo.StudentModules sm ON sm.ModuleId = c.ModuleId AND sm.StudentId = @StudentId
    JOIN dbo.Users me ON me.UserId = @StudentId
    LEFT JOIN dbo.Users u ON u.UserId = c.UploadedBy
    WHERE (c.YearOfStudy IS NULL OR c.YearOfStudy = me.YearOfStudy)
      AND (c.UploaderRole = 'Lecturer' OR c.IsReviewed = 1)
    ORDER BY c.UploadedAt DESC;
END
GO

/* ---------- "My modules" for tutors: each module and year they tutor ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutor_ModuleOverview @TutorId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester, m.Description, tm.YearOfStudy, tm.AssignedAt,
           l.FirstName + ' ' + l.LastName AS LecturerName, l.Email AS LecturerEmail,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users s ON s.UserId = sm.StudentId
             WHERE sm.ModuleId = m.ModuleId AND s.YearOfStudy = tm.YearOfStudy AND s.IsActive = 1) AS StudentCount,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots x WHERE x.TutorId = @TutorId AND x.IsBooked = 0 AND x.StartTime > dbo.fn_SaNow()) AS OpenSlots,
           (SELECT COUNT(*) FROM dbo.Tutorials t WHERE t.TutorId = @TutorId AND t.ModuleId = m.ModuleId AND t.EndTime > dbo.fn_SaNow()) AS UpcomingTutorials,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.TutorId = @TutorId AND b.ModuleId = m.ModuleId
             AND b.Status IN ('Pending','Confirmed') AND b.StartTime > dbo.fn_SaNow()) AS UpcomingSessions,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.TutorId = @TutorId AND b.ModuleId = m.ModuleId AND b.Status = 'Completed') AS CompletedSessions,
           (SELECT COUNT(*) FROM dbo.CourseContent c WHERE c.ModuleId = m.ModuleId AND (c.YearOfStudy IS NULL OR c.YearOfStudy = tm.YearOfStudy)) AS ContentCount,
           STUFF((SELECT N', ' + o.FirstName + N' ' + o.LastName FROM dbo.TutorModules t2 JOIN dbo.Users o ON o.UserId = t2.TutorId
                  WHERE t2.ModuleId = m.ModuleId AND t2.YearOfStudy = tm.YearOfStudy AND t2.TutorId <> @TutorId
                  FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'') AS OtherTutors
    FROM dbo.TutorModules tm
    JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    LEFT JOIN dbo.Users l ON l.UserId = m.LecturerId
    WHERE tm.TutorId = @TutorId
    ORDER BY tm.YearOfStudy, m.Semester, m.Code;
END
GO

/* ---------- "My modules" for lecturers: their modules, then the tutors on each ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Lecturer_ModuleOverview @LecturerId INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleId, m.Code, m.Name, m.YearLevel, m.Semester, m.Description, m.TutorsNeeded,
           STUFF((SELECT N', ' + c.Code FROM dbo.CourseModules cm JOIN dbo.Courses c ON c.CourseId = cm.CourseId
                  WHERE cm.ModuleId = m.ModuleId FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'') AS Courses,
           (SELECT COUNT(*) FROM dbo.StudentModules sm JOIN dbo.Users s ON s.UserId = sm.StudentId WHERE sm.ModuleId = m.ModuleId AND s.IsActive = 1) AS StudentCount,
           (SELECT COUNT(*) FROM dbo.TutorModules tm WHERE tm.ModuleId = m.ModuleId) AS TutorCount,
           (SELECT COUNT(*) FROM dbo.Tutorials t WHERE t.ModuleId = m.ModuleId) AS TutorialCount,
           (SELECT COUNT(*) FROM dbo.CourseContent c WHERE c.ModuleId = m.ModuleId) AS ContentCount,
           (SELECT COUNT(*) FROM dbo.CourseContent c WHERE c.ModuleId = m.ModuleId AND c.UploaderRole = 'Tutor' AND c.IsReviewed = 0) AS ContentToReview,
           (SELECT CAST(100.0 * SUM(CASE WHEN a.Status IN ('Present','Late') THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) AS DECIMAL(5,1))
              FROM dbo.TutorialAttendance a JOIN dbo.Tutorials t ON t.TutorialId = a.TutorialId WHERE t.ModuleId = m.ModuleId) AS AttendanceRate
    FROM dbo.Modules m WHERE m.LecturerId = @LecturerId
    ORDER BY m.YearLevel, m.Semester, m.Code;

    SELECT tm.ModuleId, tm.TutorId, tm.YearOfStudy, u.FirstName + ' ' + u.LastName AS TutorName, u.Email AS TutorEmail,
           (SELECT COUNT(*) FROM dbo.AvailabilitySlots x WHERE x.TutorId = tm.TutorId AND x.IsBooked = 0 AND x.StartTime > dbo.fn_SaNow()) AS OpenSlots,
           (SELECT COUNT(*) FROM dbo.Bookings b WHERE b.TutorId = tm.TutorId AND b.ModuleId = tm.ModuleId AND b.Status = 'Completed') AS CompletedSessions
    FROM dbo.TutorModules tm JOIN dbo.Users u ON u.UserId = tm.TutorId JOIN dbo.Modules m ON m.ModuleId = tm.ModuleId
    WHERE m.LecturerId = @LecturerId
    ORDER BY u.LastName, u.FirstName;
END
GO

/* ---------- When a tutorial is over, everyone on the register who didn't check in is marked absent ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_Tutorials_AutoMarkAbsent AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Now DATETIME2 = dbo.fn_SaNow();
    DECLARE @Marked TABLE (TutorialId INT, StudentId INT);

    BEGIN TRAN;
    INSERT dbo.TutorialAttendance (TutorialId, StudentId, Status, Method, MarkedAt)
    OUTPUT inserted.TutorialId, inserted.StudentId INTO @Marked
    SELECT tu.TutorialId, u.UserId, 'Absent', 'Auto', @Now
    FROM dbo.Tutorials tu
    JOIN dbo.StudentModules sm ON sm.ModuleId = tu.ModuleId
    JOIN dbo.Users u ON u.UserId = sm.StudentId AND u.YearOfStudy = tu.YearOfStudy AND u.Role = 'Student' AND u.IsActive = 1
    WHERE tu.EndTime <= @Now
      AND u.CreatedAt <= tu.StartTime          -- not for tutorials held before the student joined
      AND NOT EXISTS (SELECT 1 FROM dbo.TutorialAttendance a WITH (UPDLOCK, HOLDLOCK)
                      WHERE a.TutorialId = tu.TutorialId AND a.StudentId = u.UserId);

    INSERT dbo.ActivityLog (UserId, Message, Link)
    SELECT mk.StudentId, N'You were marked absent from "' + tu.Title + N'" (' + m.Code + N') on '
                         + FORMAT(tu.StartTime, 'ddd dd MMM HH:mm') + N' because you didn''t check in.', N'/Student/Attendance'
    FROM @Marked mk JOIN dbo.Tutorials tu ON tu.TutorialId = mk.TutorialId JOIN dbo.Modules m ON m.ModuleId = tu.ModuleId;
    COMMIT;

    SELECT COUNT(*) AS Marked FROM @Marked;
END
GO

PRINT 'TutorConnect stored procedures created.';
