using System;
using System.Configuration;
using System.Globalization;
using System.Web;

namespace TutorConnect.Services
{
    /// <summary>Typed access to appSettings in Web.config.</summary>
    public static class AppConfig
    {
        public static string Get(string key, string fallback = null)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        public static bool GetBool(string key, bool fallback = false)
        {
            bool result;
            return bool.TryParse(Get(key), out result) ? result : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            int result;
            return int.TryParse(Get(key), out result) ? result : fallback;
        }

        public static decimal HourlyRate
        {
            get
            {
                decimal rate;
                return decimal.TryParse(Get("HourlyRate"), NumberStyles.Number, CultureInfo.InvariantCulture, out rate) ? rate : 105m;
            }
        }

        /// <summary>
        /// Public base URL used in emails and PayFast callbacks. Falls back to the current request's host.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                var configured = Get("SiteBaseUrl");
                if (!string.IsNullOrEmpty(configured)) return configured.TrimEnd('/');
                var ctx = HttpContext.Current;
                if (ctx == null) return "http://localhost";
                // Behind a tunnel or proxy (ngrok, Visual Studio dev tunnels) use the public address the browser used.
                var fwdHost = ctx.Request.Headers["X-Original-Host"] ?? ctx.Request.Headers["X-Forwarded-Host"];
                if (!string.IsNullOrWhiteSpace(fwdHost))
                {
                    var proto = ctx.Request.Headers["X-Forwarded-Proto"];
                    return (string.IsNullOrWhiteSpace(proto) ? "https" : proto.Split(',')[0].Trim()) + "://" + fwdHost.Split(',')[0].Trim()
                           + (ctx.Request.ApplicationPath ?? "").TrimEnd('/');
                }
                var url = ctx.Request.Url;
                return url.GetLeftPart(UriPartial.Authority) + (ctx.Request.ApplicationPath ?? "").TrimEnd('/');
            }
        }
    }
}
