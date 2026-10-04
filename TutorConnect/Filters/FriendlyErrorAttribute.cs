using System;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Services;

namespace TutorConnect.Filters
{
    /// <summary>
    /// Global safety net: any exception thrown by an action becomes a clear message instead of an error page.
    /// Form posts go back to the page they came from with the message shown; AJAX calls get JSON.
    /// On localhost the underlying error text is included so problems are easy to diagnose.
    /// </summary>
    public class FriendlyErrorAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext ctx)
        {
            if (ctx.ExceptionHandled) return;

            var ex = ctx.Exception;
            var http = ctx.HttpContext;
            var request = http.Request;
            var local = request.IsLocal;
            var where = ctx.RouteData.Values["controller"] + "/" + ctx.RouteData.Values["action"];
            AppLog.Error(where, ex);

            string message, userMessage;
            if (ex is HttpAntiForgeryException)
                message = "This page was open too long or your session restarted. Please try that again.";
            else if (ex is HttpRequestValidationException)
                message = "Text can't include HTML tags. Remove any < or > characters and try again.";
            else if (Db.IsUserError(ex, out userMessage))
                message = userMessage;
            else if (ex.GetBaseException() is SqlException && DatabaseSetup.Enabled && !request.IsAjaxRequest() && !DatabaseSetup.Check().Ready)
            {
                DatabaseReadyAttribute.Reset();
                http.Response.Clear();
                ctx.Result = new RedirectResult(new UrlHelper(ctx.RequestContext).Action("Index", "Setup"));
                ctx.ExceptionHandled = true;
                return;
            }
            else if (ex.GetBaseException() is SqlException)
                message = "The database couldn't be reached or returned an error. Check the connection string in Web.config and that all three SQL scripts have been run."
                          + (local ? " Details: " + ex.GetBaseException().Message : "");
            else
                message = "That didn't work because of an error on our side. It has been logged."
                          + (local ? " Details: " + ex.GetBaseException().Message : "");

            http.Response.Clear();
            http.Response.TrySkipIisCustomErrors = true;

            if (request.IsAjaxRequest())
            {
                http.Response.StatusCode = 500;
                ctx.Result = new JsonResult { Data = new { ok = false, message }, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
            else
            {
                var referrer = request.UrlReferrer;
                var canGoBack = referrer != null
                    && string.Equals(referrer.Host, request.Url.Host, StringComparison.OrdinalIgnoreCase)
                    && referrer.PathAndQuery != request.RawUrl;

                if (canGoBack)
                {
                    ctx.Controller.TempData["Flash"] = message;
                    ctx.Controller.TempData["FlashType"] = "error";
                    ctx.Result = new RedirectResult(referrer.PathAndQuery);
                }
                else
                {
                    http.Response.StatusCode = 500;
                    var view = new ViewResult { ViewName = "~/Views/Shared/Error.cshtml" };
                    view.ViewData["Message"] = message;
                    ctx.Result = view;
                }
            }
            ctx.ExceptionHandled = true;
        }
    }
}
