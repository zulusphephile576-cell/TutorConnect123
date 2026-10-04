using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace TutorConnect.Filters
{
    /// <summary>
    /// Session-based role gate. Unauthenticated or wrong-role users are sent to the login page;
    /// AJAX callers get a 401 instead of an HTML redirect.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RoleAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles ?? new string[0];
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)) return;

            var session = filterContext.HttpContext.Session;
            var userId = session?["UserId"] as int?;
            var role = session?["Role"] as string;

            if (userId.HasValue && (_roles.Length == 0 || _roles.Contains(role))) return;

            var request = filterContext.HttpContext.Request;
            if (request.IsAjaxRequest())
            {
                filterContext.Result = new HttpStatusCodeResult(401, "Please log in again.");
                return;
            }

            if (userId.HasValue)
            {
                filterContext.Controller.TempData["Flash"] = "That page isn't available for your role.";
                filterContext.Controller.TempData["FlashType"] = "error";
            }

            var returnUrl = request.HttpMethod == "GET" ? request.RawUrl : null;
            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
            {
                { "controller", "Account" },
                { "action", "Login" },
                { "returnUrl", returnUrl }
            });
        }
    }

    /// <summary>Stops browsers caching authenticated pages (so Back after logout doesn't show data).</summary>
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var cache = filterContext.HttpContext.Response.Cache;
            cache.SetCacheability(HttpCacheability.NoCache);
            cache.SetNoStore();
            cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            base.OnResultExecuting(filterContext);
        }
    }
}
