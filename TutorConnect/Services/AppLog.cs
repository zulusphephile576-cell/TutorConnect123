using System;
using System.IO;
using System.Web.Hosting;

namespace TutorConnect.Services
{
    /// <summary>Very small file logger (App_Data/logs/yyyy-MM-dd.log). Never throws.</summary>
    public static class AppLog
    {
        private static readonly object Sync = new object();

        public static string DataPath(string relative)
        {
            var root = HostingEnvironment.MapPath("~/App_Data") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            return Path.Combine(root, relative);
        }

        public static void Write(string category, string message)
        {
            try
            {
                var dir = DataPath("logs");
                Directory.CreateDirectory(dir);
                var line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}", TutorConnect.Helpers.Clock.Now, category, message, Environment.NewLine);
                lock (Sync) File.AppendAllText(Path.Combine(dir, TutorConnect.Helpers.Clock.Now.ToString("yyyy-MM-dd") + ".log"), line);
            }
            catch
            {
                // Logging must never break a request.
            }
        }

        public static void Error(string context, Exception ex)
        {
            Write("ERROR", context + ": " + ex);
        }

        public static void AppendTo(string fileName, string text)
        {
            try
            {
                var path = DataPath(fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                lock (Sync) File.AppendAllText(path, text + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
