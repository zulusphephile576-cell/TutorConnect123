using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Helpers;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    /// <summary>
    /// AI study assistant powered by Claude. Conversations are kept only in the user's session (never in the
    /// database) and only the module and year are shared with the AI, never names or student numbers.
    /// </summary>
    [RoleAuthorize("Student", "Tutor")]
    public class AssistantController : BaseController
    {
        public class ModuleOption
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public int? Year { get; set; }
        }

        private List<ModuleOption> MyModules()
        {
            if (CurrentRole == "Tutor")
                return Db.List<TutorAssignment>("sp_Tutor_Assignments", Db.P("@TutorId", CurrentUserId))
                    .Select(a => new ModuleOption { Code = a.ModuleCode, Name = a.ModuleName, Year = a.YearOfStudy })
                    .GroupBy(m => m.Code).Select(g => g.First()).ToList();
            return Db.List<Module>("sp_Student_Modules", Db.P("@StudentId", CurrentUserId))
                .Select(m => new ModuleOption { Code = m.Code, Name = m.Name, Year = m.YearLevel }).ToList();
        }

        private string HistoryKey(string module) { return "AiChat:" + (module ?? ""); }

        private List<AiService.Turn> History(string module)
        {
            var list = Session[HistoryKey(module)] as List<AiService.Turn>;
            if (list == null) { list = new List<AiService.Turn>(); Session[HistoryKey(module)] = list; }
            return list;
        }

        private int UsedThisHour()
        {
            try { return Db.ScalarInt("sp_Ai_RecentCount", Db.P("@UserId", CurrentUserId)); }
            catch (Exception ex) { AppLog.Error("Ai count", ex); return 0; }
        }

        public ActionResult Index(string module)
        {
            List<ModuleOption> modules;
            try { modules = MyModules(); }
            catch (Exception ex) { AppLog.Error("Assistant modules", ex); modules = new List<ModuleOption>(); }

            var selected = modules.FirstOrDefault(m => m.Code == module) ?? modules.FirstOrDefault();
            ViewBag.Modules = modules;
            ViewBag.Selected = selected;
            ViewBag.History = selected == null ? new List<AiService.Turn>() : History(selected.Code);
            ViewBag.Configured = true;
            ViewBag.Provider = AiService.Current();
            ViewBag.Limit = AiService.HourlyLimit;
            ViewBag.Remaining = Math.Max(0, AiService.HourlyLimit - UsedThisHour());
            ViewBag.Modes = CurrentRole == "Tutor" ? AiService.TutorModes : AiService.StudentModes;
            return View();
        }

        /// <summary>AJAX: ask a question. Always answers with JSON { ok, answer | message, remaining }.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Ask(string module, string mode, string message)
        {
            try
            {
                message = (message ?? "").Trim();
                var modes = CurrentRole == "Tutor" ? AiService.TutorModes : AiService.StudentModes;
                if (!modes.Contains(mode)) mode = "explain";
                if (message.Length == 0) return JsonError("Type a question first.");
                if (message.Length > 2000) return JsonError("Keep your question under 2 000 characters.");

                var m = MyModules().FirstOrDefault(x => x.Code == module);
                if (m == null) return JsonError("Choose one of your modules first.");

                var used = UsedThisHour();
                if (used >= AiService.HourlyLimit)
                    return JsonError("You've reached " + AiService.HourlyLimit + " questions this hour. The limit resets on a rolling basis, so try again a little later.", 429);

                var history = History(m.Code);
                var conversation = AiService.Trim(history.Concat(new[] { new AiService.Turn { Role = "user", Text = message } }).ToList());
                var system = AiService.SystemPrompt(CurrentRole, mode, m.Code, m.Name, m.Year, Clock.Today);

                AiService.Provider usedBy;
                var answer = AiService.AskWithFallback(system, conversation, mode, m.Code, m.Name, Clock.Today, out usedBy);

                history.Add(new AiService.Turn { Role = "user", Text = message });
                history.Add(new AiService.Turn { Role = "assistant", Text = answer });
                Session[HistoryKey(m.Code)] = AiService.Trim(history, 20);

                try { Db.Exec("sp_Ai_LogRequest", Db.P("@UserId", CurrentUserId), Db.P("@Mode", mode), Db.P("@ModuleCode", m.Code)); }
                catch (Exception ex) { AppLog.Error("Ai log", ex); }

                return Json(new { ok = true, answer, provider = usedBy.Label + (usedBy.Model == null ? "" : " (" + usedBy.Model + ")"), remaining = Math.Max(0, AiService.HourlyLimit - used - 1) });
            }
            catch (Exception ex)
            {
                AppLog.Error("Assistant/Ask", ex);
                return JsonError("Something went wrong while asking the assistant. Please try again.", 500);
            }
        }

        /// <summary>Starts a new conversation for a module.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reset(string module)
        {
            Session.Remove(HistoryKey(module));
            return RedirectToAction("Index", new { module });
        }
    }
}
