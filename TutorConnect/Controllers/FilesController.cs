using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    /// <summary>Streams uploaded files from App_Data after checking the caller may see them.</summary>
    public class FilesController : BaseController
    {
        [RoleAuthorize("Student", "Tutor", "Lecturer", "Admin")]
        [ActionName("Content")]
        public ActionResult ContentFile(int id)
        {
            var item = Db.Single<ContentItem>("sp_Content_Get", Db.P("@ContentId", id));
            if (item == null) return HttpNotFound();

            var allowed = CurrentRole == "Admin"
                || (CurrentRole == "Lecturer" && item.LecturerId == CurrentUserId)
                || (CurrentRole == "Tutor" && Db.List<ContentItem>("sp_Content_ForTutor", Db.P("@TutorId", CurrentUserId)).Any(c => c.ContentId == id))
                // students: only material for their own modules and year that has been published to them
                || (CurrentRole == "Student" && Db.List<ContentItem>("sp_Content_ForStudent", Db.P("@StudentId", CurrentUserId)).Any(c => c.ContentId == id));
            if (!allowed) return new HttpStatusCodeResult(403);

            return Stream(item.FilePath, item.FileName);
        }

        [RoleAuthorize("Lecturer", "Admin")]
        public ActionResult Application(int id, string doc)
        {
            var app = Db.Single<TutorApplication>("sp_Application_Select", Db.P("@ApplicationId", id), Db.P("@Email", null),
                Db.P("@LecturerId", CurrentRole == "Lecturer" ? (object)CurrentUserId : null));
            if (app == null) return HttpNotFound();

            var path = doc == "transcript" ? app.TranscriptPath : doc == "cv" ? app.CvPath : app.IdDocumentPath;
            var label = (doc == "transcript" ? "Transcript-" : doc == "cv" ? "CV-" : "StudentCard-") + app.LastName + "-" + app.StudentNumber;
            return Stream(path, label + System.IO.Path.GetExtension(path ?? ""));
        }

        private ActionResult Stream(string relative, string downloadName)
        {
            var full = FileStorage.FullPath(relative);
            if (full == null) return HttpNotFound();
            return File(full, FileStorage.MimeType(downloadName), downloadName);
        }
    }
}
