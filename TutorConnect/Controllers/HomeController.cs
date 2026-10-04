using System.Web.Mvc;

namespace TutorConnect.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            if (IsLoggedIn) return DashboardFor(CurrentRole);
            return View();
        }

        /// <summary>POPIA privacy notice (public).</summary>
        public ActionResult Privacy()
        {
            return View();
        }

        public ActionResult Error()
        {
            return View("Error");
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }
    }
}
