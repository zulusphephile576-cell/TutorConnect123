using System.Diagnostics;
using System.Web.Mvc;
using TutorConnect.Services;

namespace TutorConnect.Filters
{
    /// <summary>
    /// Measures each page (action plus rendering). Anything slower than Perf:SlowMs (default 1500 ms) is written to
    /// App_Data/logs so slow pages can be found, and the time is sent in a Server-Timing header (visible in the
    /// browser's developer tools, Network tab).
    /// </summary>
    public class RequestTimingAttribute : ActionFilterAttribute
    {
        private const string Key = "__tc_timer";

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            ctx.HttpContext.Items[Key] = Stopwatch.StartNew();
        }

        public override void OnResultExecuted(ResultExecutedContext ctx)
        {
            var sw = ctx.HttpContext.Items[Key] as Stopwatch;
            if (sw == null || ctx.IsChildAction) return;
            sw.Stop();
            var ms = sw.ElapsedMilliseconds;
            try { ctx.HttpContext.Response.AppendHeader("Server-Timing", "app;dur=" + ms); } catch { /* headers already sent */ }
            if (ms >= AppConfig.GetInt("Perf:SlowMs", 1500))
                AppLog.Write("SLOW", ms + " ms  " + ctx.HttpContext.Request.HttpMethod + " " + ctx.HttpContext.Request.RawUrl);
        }
    }
}
