using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using TutorConnect.Models;

namespace TutorConnect.Services
{
    /// <summary>
    /// PayFast hosted-checkout integration: builds the signed checkout form and verifies ITN callbacks.
    /// Docs: https://developers.payfast.co.za/docs
    /// </summary>
    public static class PayFastService
    {
        public static bool Sandbox => AppConfig.GetBool("PayFast:Sandbox", true);

        private static string Host => Sandbox ? "sandbox.payfast.co.za" : "www.payfast.co.za";

        public static string ProcessUrl => "https://" + Host + "/eng/process";

        private static string ValidateUrl => "https://" + Host + "/eng/query/validate";

        private static string Passphrase => AppConfig.Get("PayFast:Passphrase");

        /// <summary>Builds the ordered, signed field list for the checkout form. Order matters for the signature.</summary>
        public static List<KeyValuePair<string, string>> BuildCheckoutFields(Payment payment)
        {
            var baseUrl = AppConfig.BaseUrl;
            var fields = new List<KeyValuePair<string, string>>
            {
                Pair("merchant_id", AppConfig.Get("PayFast:MerchantId", "10000100")),
                Pair("merchant_key", AppConfig.Get("PayFast:MerchantKey", "46f0cd694581a")),
                Pair("return_url", baseUrl + "/Payment/Return?reference=" + Uri.EscapeDataString(payment.PaymentRef)),
                Pair("cancel_url", baseUrl + "/Payment/Cancel?reference=" + Uri.EscapeDataString(payment.PaymentRef)),
                Pair("notify_url", baseUrl + "/Payment/Notify"),
                Pair("name_first", payment.StudentFirstName),
                Pair("name_last", payment.StudentLastName),
                Pair("email_address", payment.StudentEmail),
                Pair("m_payment_id", payment.PaymentRef),
                Pair("amount", payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
                Pair("item_name", Truncate("Tutoring: " + payment.ModuleCode + " (" + payment.Hours + (payment.Hours == 1 ? " hour)" : " hours)"), 100)),
                Pair("item_description", Truncate("One-on-one session with " + payment.TutorName + " on " + payment.SessionStart.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture), 255)),
                Pair("custom_int1", payment.PaymentId.ToString(CultureInfo.InvariantCulture))
            };

            // Blank fields must not be posted or signed.
            fields = fields.Where(f => !string.IsNullOrEmpty(f.Value)).ToList();
            fields.Add(Pair("signature", Signature(fields, Passphrase)));
            return fields;
        }

        /// <summary>
        /// Full ITN verification: signature, amount, and a server-to-server confirmation with PayFast.
        /// </summary>
        public static bool VerifyNotification(NameValueCollection form, decimal expectedAmount, out string reason)
        {
            reason = null;

            // 1. Signature: every posted field except the signature, in the order received.
            var posted = new List<KeyValuePair<string, string>>();
            foreach (string key in form.AllKeys)
            {
                if (key == null || key == "signature") continue;
                posted.Add(Pair(key, form[key] ?? ""));
            }
            var expectedSignature = Signature(posted, Passphrase);
            if (!string.Equals(expectedSignature, form["signature"], StringComparison.OrdinalIgnoreCase))
            {
                reason = "Signature mismatch";
                return false;
            }

            // 2. Amount matches what we asked for.
            decimal gross;
            if (!decimal.TryParse(form["amount_gross"], NumberStyles.Number, CultureInfo.InvariantCulture, out gross) || Math.Abs(gross - expectedAmount) > 0.01m)
            {
                reason = "Amount mismatch";
                return false;
            }

            // 3. Ask PayFast to confirm the data really came from them.
            if (AppConfig.GetBool("PayFast:ValidateWithServer", true))
            {
                try
                {
                    var paramString = ParamString(posted, null);
                    using (var client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                        var response = client.UploadString(ValidateUrl, paramString);
                        if (!string.Equals((response ?? "").Trim(), "VALID", StringComparison.OrdinalIgnoreCase))
                        {
                            reason = "PayFast server validation returned '" + response + "'";
                            return false;
                        }
                    }
                }
                catch (WebException ex)
                {
                    reason = "Could not reach PayFast to validate: " + ex.Message;
                    return false;
                }
            }
            return true;
        }

        public static string Signature(IEnumerable<KeyValuePair<string, string>> fields, string passphrase)
        {
            var data = ParamString(fields, passphrase);
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        private static string ParamString(IEnumerable<KeyValuePair<string, string>> fields, string passphrase)
        {
            var sb = new StringBuilder();
            foreach (var f in fields)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(f.Key).Append('=').Append(PhpUrlEncode((f.Value ?? "").Trim()));
            }
            if (!string.IsNullOrEmpty(passphrase))
                sb.Append("&passphrase=").Append(PhpUrlEncode(passphrase.Trim()));
            return sb.ToString();
        }

        /// <summary>Matches PHP's urlencode(): spaces become '+', hex digits are upper case.</summary>
        public static string PhpUrlEncode(string value)
        {
            var sb = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(value ?? ""))
            {
                var c = (char)b;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')
                    sb.Append(c);
                else if (c == ' ')
                    sb.Append('+');
                else
                    sb.Append('%').Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        private static KeyValuePair<string, string> Pair(string k, string v)
        {
            return new KeyValuePair<string, string>(k, v);
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        public static string NewReference()
        {
            return "TC" + TutorConnect.Helpers.Clock.Now.ToString("yyMMddHHmmss") + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }
    }
}
