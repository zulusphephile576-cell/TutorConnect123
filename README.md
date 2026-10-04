# TutorConnect

A role-based tutoring platform for students, tutors, lecturers and administrators, built with
**ASP.NET MVC 5 on .NET Framework 4.8**, **ADO.NET with stored procedures only** and **SQL Server**.

## What's in the box

```
TutorConnect.sln
Database/
  01_Schema.sql            tables, constraints, indexes, table type
  02_StoredProcedures.sql  every database call the app makes (92 procedures)
  03_SeedData.sql          demo courses, modules, users, slots and a tutorial
  04_Upgrade.sql           brings an older database up to date, keeping its data
  05_MoreDemoUsers.sql     adds 34 more demo people (safe to run again)
TutorConnect/              the web project
  Controllers/             Account, Student, Tutor, Lecturer, Admin, Payment, TutorApplication, Files, Notifications
  Data/Db.cs               thin ADO.NET helper (stored procedures, row-to-object mapping)
  Services/                PayFast, email, BCrypt, encryption, file storage, payouts
  Views/                   Razor views for every role
  Content/site.css         design system (Poppins, indigo/purple, light + dark)
  Scripts/site.js          filters, reminders, live registers, QR, booking calculator
```

## Requirements

- Visual Studio 2019 or 2022 with the **ASP.NET and web development** workload
- .NET Framework 4.8 developer pack
- SQL Server 2016 SP1 or later (LocalDB, Express or full). The scripts use `CREATE OR ALTER`.

## Getting started

### 1. Make sure you have a SQL Server

Visual Studio's "ASP.NET and web development" workload normally installs **SQL Server Express LocalDB**,
which is all you need. To check, open a Command Prompt and run:

```
sqllocaldb info
```

- If it prints `MSSQLLocalDB`, you're set. Start it with `sqllocaldb start MSSQLLocalDB`.
- If the command isn't recognised, open the **Visual Studio Installer**, choose **Modify**, then
  **Individual components**, tick **SQL Server Express LocalDB**, and install.
- Already have SQL Server Express or a full SQL Server? That works too; see step 2.

### 2. Point the app at your server

Open `TutorConnect/Web.config` and find `connectionStrings`. Use the line that matches your setup:

| You have | Connection string |
|---|---|
| LocalDB (default) | `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=TutorConnect;Integrated Security=True` |
| SQL Server Express | `Data Source=.\SQLEXPRESS;Initial Catalog=TutorConnect;Integrated Security=True` |
| Full SQL Server | `Data Source=localhost;Initial Catalog=TutorConnect;Integrated Security=True` |

The Data Source is the same server name you'd type into SQL Server Management Studio.

### 3. Create the database: the easy way (built-in setup page)

1. Open `TutorConnect.sln` and press **Ctrl+F5** (Start Without Debugging).
2. Click **Create a student account** or **Sign in**. While the database is missing, the app takes you to
   **/Setup** automatically (you can also type `http://localhost:44380/Setup`).
3. The checklist shows whether SQL Server is reachable. When it is, leave **Include demo data** on and
   press **Install database**. It runs the three scripts in the `Database` folder for you.
4. Sign in with one of the demo accounts below.

Once the database is installed, /Setup only opens for a signed-in administrator, because reinstalling
erases all data. Set `Setup:Enabled` to `false` in Web.config on a real server.

### 3 (alternative). Create the database by hand in SSMS

1. Open **SQL Server Management Studio** and connect to your server
   (for LocalDB, the server name is `(localdb)\MSSQLLocalDB`).
2. **File, Open, File** and choose `Database/01_Schema.sql`. Press **Execute** (F5).
   It creates the `TutorConnect` database and all tables.
3. Do the same for `Database/02_StoredProcedures.sql`.
4. Do the same for `Database/03_SeedData.sql` (demo data; skip it for an empty system).
5. Refresh **Databases** in Object Explorer; you should see TutorConnect with its tables and stored procedures.

Running the scripts again starts from a clean slate (01 drops and recreates everything).

**Updating an existing database without losing data:** run `Database/04_Upgrade.sql`, then run
`02_StoredProcedures.sql` again. The app does this for you automatically on startup when it detects
an older database, and the /Setup page has an "Update and keep my data" button.

### 4. Run it

Press **Ctrl+F5**. Use **F5** (with debugging) only when you want to step through code: in debug mode,
Visual Studio pauses whenever an error is thrown, even ones the app handles. If it pauses, press
**Continue** and the app shows its own message.

### Demo logins

Pick the matching tab on the sign-in page.

| Role     | Email                         | Password     |
|----------|-------------------------------|--------------|
| Admin    | admin@tutorconnect.ac.za      | Admin@123    |
| Lecturer | lecturer@tutorconnect.ac.za   | Password@123 |
| Tutor    | tutor@tutorconnect.ac.za      | Password@123 |
| Student  | student@tutorconnect.ac.za    | Password@123 |

`liam@tutorconnect.ac.za` and `ayesha@tutorconnect.ac.za` are extra students with the same password.
The demo tutor has no banking details on purpose, so you can watch a payout wait and then send
automatically once they're added.

### More demo users

`Database/05_MoreDemoUsers.sql` adds 1 administrator, 3 lecturers, 6 tutors (with modules and booking slots)
and 24 students across both courses, without deleting anything. Admin, Users, **Add demo users** runs it and also
gives every demo tutor banking details so payments to them work. Every account's password is `Password@123`,
and emails follow `firstname.lastname@tutorconnect.ac.za` (for example `zinhle.mkhize@tutorconnect.ac.za`).

## Extra features

- **Weekly schedule** for students and tutors: sessions, tutorials and open slots on a calendar, with week navigation
  and a list view on phones.
- **Notification centre** in the top bar: the latest activity with an unread count, marked as read when opened.
- **Quick jump** (Ctrl+K, or the search box in the top bar): type a page name or action to go straight there.
- **Password tools**: show/hide on every password field and a strength meter where you choose a new one.
- **Setup page** (/Setup): connection check and one-click database install.
- **Double-submit protection**: buttons lock with a spinner after the first click.

## AI study assistant

It always answers, using the best engine available:

1. **Claude**, if `Anthropic:ApiKey` is set in Web.config (paid, best quality).
2. **A free local AI** if Ollama is running on the same computer: install it from ollama.com, run
   `ollama pull llama3.2`, and refresh the page. It's detected automatically.
3. **The offline study helper** otherwise: guided explanations, practice questions, dated study plans
   and tutorial outlines built from the question.

## Running the tests

**Unit tests (65):** in Visual Studio open **Test, Test Explorer** and click **Run All**. They need no database.

**API tests (Postman):** run the web app with Ctrl+F5, then in Postman choose **Import** and pick
`Testing/TutorConnect.postman_collection.json`. Set the `baseUrl` variable to the address Visual Studio
opened (for example `http://localhost:44380`), then right-click the collection and choose **Run collection**.

## Configuration (Web.config → appSettings)

| Setting | Purpose |
|---|---|
| `HourlyRate` | Price per hour. Default `105` (R105). |
| `SiteBaseUrl` | Public URL used in emails and PayFast callbacks. Empty = current host. |
| `PayFast:MerchantId`, `PayFast:MerchantKey`, `PayFast:Passphrase` | Defaults are PayFast's public sandbox merchant. |
| `PayFast:Sandbox` | `true` uses sandbox.payfast.co.za. |
| `PayFast:ValidateWithServer` | Confirms every notification with PayFast before trusting it. |
| `PayFastSimulateItn` | **Development only.** See below. |
| `PayoutProvider` | `Logging` (default). See *Payouts*. |
| `Setup:Enabled` | Turns the /Setup page on or off. Default `true`; set `false` in production. |
| `Smtp:*` | Gmail SMTP. While `Smtp:Enabled` is `false`, emails are written to `App_Data/emails.log`. |

**Machine key.** `Web.config` contains a generated `machineKey`. It encrypts tutors' bank account numbers.
Generate your own before deploying and keep it stable: changing it makes stored account numbers unreadable.

**Gmail.** Turn on 2-step verification for the sending account, create an *App password*,
put it in `Smtp:Pass`, and set `Smtp:Enabled` to `true`.

## Payments (PayFast)

1. The student picks a module, a slot and a number of hours. A `Pending` payment row is created.
2. They're sent to PayFast's hosted checkout with a signed form.
3. PayFast calls `/Payment/Notify` (the ITN). The app checks the signature, the amount, and asks
   PayFast's server to confirm it. Only then does `sp_Payment_Complete` create the booking.
   That procedure is idempotent, so repeated notifications never create duplicate bookings.
4. The booking arrives as a request that the tutor approves or declines.

**Local testing.** PayFast can't reach `localhost`, so the notification never arrives on a dev machine.
With `PayFastSimulateItn=true` (and sandbox mode on), returning from the sandbox completes the payment
locally, and the checkout page shows a *Simulate a successful payment* button. To test the real
notification flow, expose the site with a tunnel such as ngrok, put that URL in `SiteBaseUrl`,
and set `PayFastSimulateItn` to `false`. Always set it to `false` in production.

Sandbox buyer login: use any card details PayFast's sandbox shows on its page.

## Bank-payment checkout (simulated)

With `Payments:Mode` set to `BankTransfer` (the default), students pay through TutorConnect's own checkout:
choose a bank, enter the account holder, type and number, review the tutor's (masked) banking details, and authorise.
**This is a simulation for development and demonstrations: no money moves.** The booking, receipt, notifications and
tutor payout record are all created as they would be for a real payment. Only the bank name and the last four digits
of the account are stored, and the checkout never asks for banking passwords, PINs or one-time PINs.
If the tutor hasn't added banking details, payment is blocked and the tutor is notified to add them.
Set `Payments:Mode` to `PayFast` to use PayFast's hosted checkout instead.

## Payouts

When a payment completes, a payout is created and sent through `IPayoutProvider` straight away,
with no admin step. Statuses: `Queued`, `Sent`, `Failed`, `NoBankDetails`. If the tutor has no bank
details yet, the payout waits and is sent automatically when they save them.

**Important:** the included `LoggingPayoutProvider` only records the transfer instruction in
`App_Data/payouts.log`. **No money moves.** PayFast collects payments but does not pay out to third
parties, so a licensed payout provider (for example Stitch) is needed before going live. Implement
`Services/Payouts/IPayoutProvider.cs`, add a case in `PayoutProviderFactory`, and change the
`PayoutProvider` setting. Nothing else in the app needs to change.

## Design decisions worth knowing

- **Accounts are unique per email and role.** A student who becomes a tutor keeps their student account
  and gets a separate tutor account with the same email; the sign-in tabs choose which one.
- **Module registration follows year of study.** When a student registers, they're enrolled in every module
  linked to their course **at their year level**, not every module in the course. Changing a student's
  course or year (Admin, Users) re-registers them.
- **One tutor per module and year.** Assigning a tutor to a module/year that's already covered replaces
  the previous tutor.
- **A lecturer's modules** are the modules an administrator linked to them under Modules.
- **Deleting users.** Users with payment history are deactivated instead of deleted, so financial records stay intact.
- **Bank account numbers** are encrypted with the machine key; only the last four digits are shown anywhere.
- **Uploads** live in `App_Data/uploads` (never served directly) and are streamed through `FilesController`
  after a role check.
- **QR check-in** always uses the signed-in student's identity from the session. The QR code only identifies
  the tutorial. Check-in opens 30 minutes before the start; more than 15 minutes after the start counts as late.
- **Reminders** are in-app toasts polled every minute for sessions and tutorials starting within 20 minutes.
- **Views use C# 5 syntax** (no `$""`, `?.`, `nameof`) so they compile without the Roslyn CodeDom package.
  Controllers and services use C# 7.3.
- Every form posts with an anti-forgery token, and every page behind sign-in is served with no-cache headers.

## Not built (listed as future enhancements in the specification)

Session ratings and reviews, in-app messaging, waitlists, email reminders, PDF export, an audit log,
calendar export, refunds, and a real payout provider integration.

## Troubleshooting

- **"Could not find stored procedure"**: run `02_StoredProcedures.sql` against the right database.
- **Login always fails for demo users**: make sure `03_SeedData.sql` ran, and that you're on the right role tab.
- **Payment stays on "Waiting for PayFast"**: the notification can't reach your machine. Use
  `PayFastSimulateItn=true` locally or a tunnel.
- **Errors**: details are logged to `App_Data/logs/yyyy-MM-dd.log`.
