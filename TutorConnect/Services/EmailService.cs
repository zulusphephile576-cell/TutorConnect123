using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace TutorConnect.Services
{
    /// <summary>
    /// Sends branded HTML email over SMTP (Gmail by default). When Smtp:Enabled is false,
    /// messages are written to App_Data/emails.log instead so the app is usable offline.
    /// </summary>
    public static class EmailService
    {
        /// <summary>True when Smtp:Enabled is on, so emails really go out (otherwise they're written to App_Data/emails.log).</summary>
        public static bool Enabled => AppConfig.GetBool("Smtp:Enabled");

        /// <summary>
        /// The mail server to use. Common mistakes are corrected: an email address typed as the host (e.g.
        /// "name@gmail.com") becomes that provider's server, and an empty host defaults to Gmail.
        /// </summary>
        public static string Host
        {
            get
            {
                var h = (AppConfig.Get("Smtp:Host") ?? "").Trim();
                if (h.Length == 0) return "smtp.gmail.com";
                if (h.Contains("@"))
                {
                    var domain = h.Substring(h.IndexOf('@') + 1).ToLowerInvariant();
                    if (domain == "gmail.com" || domain == "googlemail.com") return "smtp.gmail.com";
                    if (domain == "outlook.com" || domain == "hotmail.com" || domain == "live.com") return "smtp-mail.outlook.com";
                    return "smtp." + domain;
                }
                return h;
            }
        }

        /// <summary>System.Net.Mail can't do implicit SSL (port 465), so Gmail is always used on 587 with STARTTLS.</summary>
        public static int Port
        {
            get
            {
                var p = AppConfig.GetInt("Smtp:Port", 587);
                return p == 465 ? 587 : p;
            }
        }

        public static string User => (AppConfig.Get("Smtp:User") ?? "").Trim();

        public static string From
        {
            get
            {
                var f = (AppConfig.Get("Smtp:From") ?? "").Trim();
                return f.Contains("@") ? f : User;
            }
        }

        /// <summary>Everything wrong with the email settings, in plain language. Empty when they look right.</summary>
        public static System.Collections.Generic.List<string> ConfigProblems()
        {
            var list = new System.Collections.Generic.List<string>();
            var rawHost = (AppConfig.Get("Smtp:Host") ?? "").Trim();
            var pass = (AppConfig.Get("Smtp:Pass") ?? "").Replace(" ", "");
            if (!Enabled) list.Add("Smtp:Enabled is \"false\", so emails are only saved to App_Data/emails.log. Change it to \"true\".");
            if (rawHost.Contains("@")) list.Add("Smtp:Host contains an email address (" + rawHost + "). It should be the server name: " + Host + ". The app is using " + Host + " for now; please correct Web.config.");
            if (AppConfig.GetInt("Smtp:Port", 587) == 465) list.Add("Smtp:Port is 465, which this app can't use. It's using 587 instead; set Smtp:Port to 587.");
            if (!User.Contains("@") || User.StartsWith("your.")) list.Add("Smtp:User must be your full Gmail address, for example name@gmail.com.");
            if (pass.Length == 0 || pass.StartsWith("your-")) list.Add("Smtp:Pass is empty. Paste the 16-letter App password from your Google Account.");
            else if (Host == "smtp.gmail.com" && pass.Length != 16) list.Add("Smtp:Pass is " + pass.Length + " characters long. A Gmail App password is exactly 16 letters (spaces don't count).");
            if (!AppConfig.GetBool("Smtp:EnableSsl", true)) list.Add("Smtp:EnableSsl is false. Gmail requires it: set it to true.");
            return list;
        }

        /// <summary>The last sending problem in plain language, shown to administrators. Null after a success.</summary>
        public static string LastError { get; private set; }

        public static bool Send(string to, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(to)) { LastError = "There's no email address to send to."; return false; }
            var wrapped = Wrap(subject, htmlBody);

            if (!Enabled)
            {
                AppLog.AppendTo("emails.log", string.Format("===== {0:yyyy-MM-dd HH:mm:ss} =====\nTo: {1}\nSubject: {2}\n{3}\n", TutorConnect.Helpers.Clock.Now, to, subject, htmlBody));
                return true;
            }

            try
            {
                using (var message = new MailMessage())
                using (var client = new SmtpClient(Host, Port))
                {
                    message.From = new MailAddress(From, "TutorConnect");
                    message.To.Add(to);
                    message.Subject = subject;
                    message.Body = wrapped;
                    message.IsBodyHtml = true;

                    client.EnableSsl = Host == "smtp.gmail.com" || AppConfig.GetBool("Smtp:EnableSsl", true);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.UseDefaultCredentials = false;
                    // Gmail shows app passwords in groups of four ("abcd efgh ijkl mnop"); the spaces aren't part of it.
                    client.Credentials = new NetworkCredential(User, (AppConfig.Get("Smtp:Pass") ?? "").Replace(" ", "").Trim());
                    client.Timeout = 20000;
                    client.Send(message);
                }
                AppLog.Write("EMAIL", "Sent '" + subject + "' to " + to);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Email to " + to, ex);
                LastError = Explain(ex);
                return false;
            }
        }

        /// <summary>Turns SMTP errors into advice an administrator can act on.</summary>
        public static string Explain(Exception ex)
        {
            var msg = (ex.GetBaseException().Message ?? "") + " " + (ex.Message ?? "");
            if (msg.Contains("5.7.0") || msg.Contains("5.7.8") || msg.Contains("Authentication") || msg.Contains("Username and Password not accepted"))
                return "Gmail refused the sign-in. Use a 16-character App Password (Google Account, Security, 2-Step Verification, App passwords) in Smtp:Pass, not your normal password, and check Smtp:User is the full Gmail address.";
            if (msg.Contains("5.5.1"))
                return "Gmail asked for authentication. Turn on 2-Step Verification for the account and create an App Password for Smtp:Pass.";
            if (msg.Contains("timed out") || msg.Contains("Timeout"))
                return "The mail server didn't answer in time. Check the internet connection and that Smtp:Host is smtp.gmail.com and Smtp:Port is 587.";
            if (msg.Contains("remote name could not be resolved") || msg.Contains("No such host"))
                return "The mail server name couldn't be found. Check Smtp:Host and the internet connection.";
            if (msg.Contains("mailbox unavailable") || msg.Contains("5.1.1"))
                return "The recipient address doesn't exist. Check the email address.";
            return "Sending failed: " + ex.GetBaseException().Message;
        }

        /// <summary>Fire-and-forget send for notifications that shouldn't slow the request down.</summary>
        public static void SendInBackground(string to, string subject, string htmlBody)
        {
            Task.Run(() => Send(to, subject, htmlBody));
        }

        public static string Encode(string value)
        {
            return HttpUtility.HtmlEncode(value ?? "");
        }

        private static string Wrap(string title, string body)
        {
            return "<!DOCTYPE html><html><body style=\"margin:0;background:#F4F4FB;font-family:Poppins,Segoe UI,Arial,sans-serif;color:#1F2937\">" +
                   "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"padding:32px 12px\"><tr><td align=\"center\">" +
                   "<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:14px;overflow:hidden\">" +
                   "<tr><td style=\"background:#4F46E5;padding:20px 28px;color:#ffffff;font-size:18px;font-weight:600\">TutorConnect</td></tr>" +
                   "<tr><td style=\"padding:28px;font-size:15px;line-height:1.6\"><h1 style=\"font-size:20px;margin:0 0 16px\">" + Encode(title) + "</h1>" + body + "</td></tr>" +
                   "<tr><td style=\"padding:16px 28px;background:#F9FAFB;color:#6B7280;font-size:12px\">You received this because you have a TutorConnect account or application.</td></tr>" +
                   "</table></td></tr></table></body></html>";
        }
    }
}
