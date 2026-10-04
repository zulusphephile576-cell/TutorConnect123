using System;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers.Api
{
    /// <summary>
    /// Shared plumbing for the REST API: JSON envelopes, status codes, caller resolution and JSON errors.
    /// Success: { "data": ... }   Error: { "error": { "code": "...", "message": "..." } }
    /// </summary>
    public abstract class ApiControllerBase : Controller
    {
        public const string CallerKey = "TutorConnect.ApiUser";

        /// <summary>The authenticated caller (set by <see cref="ApiAuthorizeAttribute"/>).</summary>
        protected ApiUser Caller => HttpContext.Items[CallerKey] as ApiUser;

        protected ActionResult Ok(object data, int status = 200)
        {
            return JsonStatus(new { data }, status);
        }

        protected ActionResult Error(int status, string code, string message)
        {
            return JsonStatus(new { error = new { code, message } }, status);
        }

        protected ActionResult JsonStatus(object body, int status)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            Response.Cache.SetNoStore();
            return new JsonResult { Data = body, JsonRequestBehavior = JsonRequestBehavior.AllowGet, MaxJsonLength = int.MaxValue, ContentType = "application/json" };
        }

        /// <summary>Resolves the caller from "Authorization: Bearer tc_..." or, for reads only, the website session.</summary>
        public static ApiUser ResolveCaller(ControllerContext ctx, out bool viaToken)
        {
            viaToken = false;
            var header = ctx.HttpContext.Request.Headers["Authorization"] ?? "";
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                viaToken = true;
                return ApiTokenService.Validate(header.Substring(7).Trim());
            }
            var session = ctx.HttpContext.Session;
            var id = session == null ? null : session["UserId"] as int?;
            if (!id.HasValue) return null;
            return new ApiUser
            {
                UserId = id.Value,
                Role = session["Role"] as string,
                Email = session["Email"] as string,
                FirstName = session["FirstName"] as string,
                YearOfStudy = session["YearOfStudy"] as int?,
                IsActive = true
            };
        }

        /// <summary>Every unhandled error becomes a JSON response (never an HTML error page).</summary>
        protected override void OnException(ExceptionContext ctx)
        {
            string message;
            if (Db.IsUserError(ctx.Exception, out message))
                ctx.Result = Error(409, "rule_violation", message);
            else
            {
                AppLog.Error("API " + Request.HttpMethod + " " + Request.RawUrl, ctx.Exception);
                ctx.Result = Error(500, "server_error", "The request couldn't be completed. The error has been logged."
                    + (Request.IsLocal ? " Details: " + ctx.Exception.GetBaseException().Message : ""));
            }
            ctx.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// Requires a signed-in caller (optionally in given roles). Requests that change data (POST/PUT/DELETE)
    /// must use a bearer token, which protects them from cross-site request forgery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ApiAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;
        public ApiAuthorizeAttribute(params string[] roles) { _roles = roles ?? new string[0]; }

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            bool viaToken;
            var caller = ApiControllerBase.ResolveCaller(ctx, out viaToken);
            var method = ctx.HttpContext.Request.HttpMethod;

            if (caller == null)
                ctx.Result = Fail(ctx, 401, "unauthorized", viaToken ? "The token is invalid, expired or revoked." : "Sign in, or send an API token as 'Authorization: Bearer tc_...'.");
            else if (!viaToken && method != "GET")
                ctx.Result = Fail(ctx, 401, "token_required", "Changes through the API need an API token. Create one under Settings, API access.");
            else if (_roles.Length > 0 && !_roles.Contains(caller.Role))
                ctx.Result = Fail(ctx, 403, "forbidden", "This endpoint is for " + string.Join(" or ", _roles) + " accounts.");
            else
                ctx.HttpContext.Items[ApiControllerBase.CallerKey] = caller;
        }

        private static ActionResult Fail(ActionExecutingContext ctx, int status, string code, string message)
        {
            ctx.HttpContext.Response.StatusCode = status;
            ctx.HttpContext.Response.TrySkipIisCustomErrors = true;
            if (status == 401) ctx.HttpContext.Response.AddHeader("WWW-Authenticate", "Bearer realm=\"TutorConnect\"");
            return new JsonResult { Data = new { error = new { code, message } }, JsonRequestBehavior = JsonRequestBehavior.AllowGet, ContentType = "application/json" };
        }
    }
}
