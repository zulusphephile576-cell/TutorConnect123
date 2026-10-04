using System;
using System.IO;
using System.Linq;
using System.Web;

namespace TutorConnect.Services
{
    /// <summary>
    /// Stores uploads under App_Data (which IIS never serves directly). Files are only
    /// reachable through FilesController, which checks the caller's role first.
    /// </summary>
    public static class FileStorage
    {
        public static readonly string[] DocumentExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".heic", ".webp" };
        /// <summary>CVs are written documents: PDF or Word.</summary>
        public static readonly string[] CvExtensions = { ".pdf", ".doc", ".docx" };
        public static readonly string[] ContentExtensions = { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".zip", ".jpg", ".jpeg", ".png" };

        /// <summary>Validates and saves a file. Returns the stored relative path, or null with an error message.</summary>
        public static string Save(HttpPostedFileBase file, string folder, string[] allowed, int maxMegabytes, out string error)
        {
            error = null;
            if (file == null || file.ContentLength == 0)
            {
                error = "Choose a file to upload.";
                return null;
            }
            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                error = "That file type isn't allowed. Use " + string.Join(", ", allowed.Select(a => a.TrimStart('.').ToUpperInvariant())) + ".";
                return null;
            }
            if (file.ContentLength > maxMegabytes * 1024 * 1024)
            {
                error = "Files must be " + maxMegabytes + " MB or smaller.";
                return null;
            }

            var relative = Path.Combine("uploads", folder, Guid.NewGuid().ToString("N") + ext);
            try
            {
                var full = AppLog.DataPath(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                file.SaveAs(full);
                return relative.Replace('\\', '/');
            }
            catch (Exception ex)
            {
                // e.g. App_Data is read-only, the disk is full, or a sync tool (OneDrive) has the folder locked.
                AppLog.Error("FileStorage.Save " + file.FileName, ex);
                error = "The file couldn't be saved on the server (" + ex.GetBaseException().Message + "). Try again, or ask an administrator to check the App_Data folder.";
                return null;
            }
        }

        public static string FullPath(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return null;
            var root = Path.GetFullPath(AppLog.DataPath("uploads"));
            var full = Path.GetFullPath(AppLog.DataPath(relative.Replace('/', Path.DirectorySeparatorChar)));
            // Refuse anything that escapes the uploads folder.
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
        }

        public static string MimeType(string fileName)
        {
            return MimeMapping.GetMimeMapping(fileName ?? "file.bin");
        }
    }
}
