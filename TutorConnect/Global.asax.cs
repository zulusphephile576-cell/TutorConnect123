using System;
using System.Net;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Routing;
using TutorConnect.Services;

namespace TutorConnect
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // PayFast and Gmail both require TLS 1.2.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Only Razor views are used.
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new RazorViewEngine());

            // mark students absent automatically once a tutorial is over and they didn't check in
            Services.AttendanceAutoMarker.Start();

            AntiForgeryConfig.SuppressXFrameOptionsHeader = false;
            MvcHandler.DisableMvcResponseHeader = true;
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();

            // A form bigger than maxRequestLength is rejected before MVC runs. Send the person back to the
            // form with an explanation instead of showing an error page.
            var http = ex as HttpException;
            if (http != null && (http.WebEventCode == 3004 || (http.Message ?? "").Contains("Maximum request length")))
            {
                Server.ClearError();
                var path = Request.Url.AbsolutePath;
                Response.Redirect(path + "?tooLarge=1", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (ex is HttpException && ((HttpException)ex).GetHttpCode() == 404) return;
            if (ex != null) AppLog.Error("Unhandled", ex);
        }
    }
}
