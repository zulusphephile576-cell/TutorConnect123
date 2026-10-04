using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TutorConnect.Services
{
    /// <summary>
    /// Calls the Claude Messages API (POST https://api.anthropic.com/v1/messages) for the AI study assistant.
    /// The API key lives in Web.config (Anthropic:ApiKey) and never reaches the browser.
    /// Docs: https://docs.claude.com/en/api/overview
    /// </summary>
    public static class AiService
    {
        public const string Endpoint = "https://api.anthropic.com/v1/messages";
        public const string ApiVersion = "2023-06-01";

        public class Turn
        {
            public string Role { get; set; }   // "user" or "assistant"
            public string Text { get; set; }
        }

        /// <summary>The assistant always works: Claude if a key is set, else a local Ollama model, else the offline helper.</summary>
        public static bool Configured => true;

        public static bool ClaudeConfigured => !string.IsNullOrWhiteSpace(AppConfig.Get("Anthropic:ApiKey"));

        public class Provider
        {
            public string Key { get; set; }      // claude, ollama, offline
            public string Label { get; set; }
            public string Model { get; set; }
        }

        public static string OllamaUrl => AppConfig.Get("Ollama:Url", "http://127.0.0.1:11434").TrimEnd('/');

        /// <summary>
        /// Addresses to try. "localhost" is tried as 127.0.0.1 first: on Windows, localhost resolves to IPv6 (::1) first,
        /// Ollama listens on IPv4 only, and the IPv6 attempt can take longer than the detection timeout.
        /// </summary>
        public static List<string> OllamaCandidates()
        {
            var configured = OllamaUrl;
            var list = new List<string>();
            if (configured.IndexOf("://localhost", StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(Regex.Replace(configured, "://localhost", "://127.0.0.1", RegexOptions.IgnoreCase));
            list.Add(configured);
            return list.Distinct().ToList();
        }

        private static readonly object OllamaSync = new object();
        private static DateTime _ollamaCheckedUtc = DateTime.MinValue;
        private static string _ollamaModel;   // null when Ollama isn't running or has no models
        private static string _ollamaBase;    // the address that answered

        /// <summary>Why the local AI isn't being used (shown to help set it up). Null when it works.</summary>
        public static string OllamaProblem { get; private set; }

        /// <summary>
        /// Finds a running Ollama server and a model to use (the configured one if installed, otherwise the first
        /// installed model). Checked at most every 20 seconds with a short timeout so pages never wait long.
        /// </summary>
        public static string DetectOllamaModel()
        {
            if (!AppConfig.GetBool("Ollama:Enabled", true)) { OllamaProblem = "Ollama is switched off in Web.config (Ollama:Enabled)."; return null; }
            lock (OllamaSync)
            {
                if ((DateTime.UtcNow - _ollamaCheckedUtc).TotalSeconds < 30) return _ollamaModel;
                _ollamaCheckedUtc = DateTime.UtcNow;
                _ollamaModel = null;
                OllamaProblem = "Ollama isn't running on this computer. Open Ollama from the Start menu.";
                foreach (var baseUrl in OllamaCandidates())
                {
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create(baseUrl + "/api/tags");
                        req.Timeout = 1500; req.ReadWriteTimeout = 1500;
                        req.Proxy = null;   // never send local traffic through a proxy
                        using (var res = (HttpWebResponse)req.GetResponse())
                        using (var reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                        {
                            var model = PickOllamaModel(reader.ReadToEnd(), AppConfig.Get("Ollama:Model", "llama3.2"));
                            if (model == null)
                            {
                                OllamaProblem = "Ollama is running but has no AI model yet. In a Command Prompt run: ollama pull llama3.2";
                                AppLog.Write("AI", "Ollama at " + baseUrl + " has no models installed.");
                                return null;
                            }
                            _ollamaModel = model; _ollamaBase = baseUrl; OllamaProblem = null;
                            return _ollamaModel;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("AI", "Ollama not reachable at " + baseUrl + ": " + ex.GetBaseException().Message);
                        var we = ex as WebException;
                        // "Connection refused" means nothing is listening: Ollama isn't running, so don't wait on other addresses.
                        if (we != null && we.Status == WebExceptionStatus.ConnectFailure) break;
                    }
                }
                return null;
            }
        }

        /// <summary>From GET /api/tags ({ "models": [ { "name": "llama3.2:latest" } ] }), prefer the configured model.</summary>
        public static string PickOllamaModel(string tagsJson, string preferred)
        {
            if (string.IsNullOrWhiteSpace(tagsJson)) return null;
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(tagsJson);
            object models;
            if (root == null || !root.TryGetValue("models", out models)) return null;
            var names = new List<string>();
            foreach (var m in (models as System.Collections.IEnumerable) ?? new object[0])
            {
                var d = m as Dictionary<string, object>;
                object n;
                if (d != null && d.TryGetValue("name", out n) && n != null) names.Add(Convert.ToString(n));
            }
            if (names.Count == 0) return null;
            var match = names.FirstOrDefault(n => n.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                                                  || n.StartsWith(preferred + ":", StringComparison.OrdinalIgnoreCase));
            return match ?? names[0];
        }

        /// <summary>What will answer the next question.</summary>
        public static Provider Current()
        {
            if (ClaudeConfigured) return new Provider { Key = "claude", Label = "Claude", Model = Model };
            var local = DetectOllamaModel();
            if (local != null) return new Provider { Key = "ollama", Label = "Local AI", Model = local };
            return new Provider { Key = "offline", Label = "Offline study helper", Model = null };
        }

        /// <summary>
        /// Answers with the best available engine and falls back automatically, so the student always gets a reply:
        /// Claude, then a local Ollama model, then the offline study helper.
        /// </summary>
        public static string AskWithFallback(string systemPrompt, IList<Turn> conversation, string mode, string moduleCode,
            string moduleName, DateTime today, out Provider usedBy)
        {
            string error;
            if (ClaudeConfigured)
            {
                var answer = Ask(systemPrompt, conversation, out error);
                if (answer != null) { usedBy = new Provider { Key = "claude", Label = "Claude", Model = Model }; return answer; }
                AppLog.Write("AI", "Claude unavailable (" + error + "); trying the next option.");
            }
            var local = DetectOllamaModel();
            if (local != null)
            {
                var answer = AskOllama(local, systemPrompt, conversation, out error);
                if (answer != null) { usedBy = new Provider { Key = "ollama", Label = "Local AI", Model = local }; return answer; }
                AppLog.Write("AI", "Ollama unavailable (" + error + "); using the offline helper.");
                lock (OllamaSync) _ollamaCheckedUtc = DateTime.MinValue;   // re-check next time
            }
            usedBy = new Provider { Key = "offline", Label = "Offline study helper" };
            var last = conversation.LastOrDefault(t => t.Role == "user");
            return OfflineStudyHelper.Reply(mode, moduleCode, moduleName, last == null ? "" : last.Text, today);
        }

        /// <summary>POST {Ollama:Url}/api/chat with stream=false; the reply is in message.content.</summary>
        public static string AskOllama(string model, string systemPrompt, IList<Turn> conversation, out string error)
        {
            error = null;
            var messages = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "role", "system" }, { "content", systemPrompt } } };
            messages.AddRange(conversation.Select(t => new Dictionary<string, object> { { "role", t.Role }, { "content", t.Text } }));
            var body = new Dictionary<string, object> { { "model", model }, { "messages", messages }, { "stream", false } };
            var payload = Encoding.UTF8.GetBytes(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(body));
            try
            {
                var request = (HttpWebRequest)WebRequest.Create((_ollamaBase ?? OllamaCandidates()[0]) + "/api/chat");
                request.Proxy = null;
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 180000;            // local models on a laptop can be slow
                request.ReadWriteTimeout = 180000;
                using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var text = ExtractOllamaText(reader.ReadToEnd());
                    if (string.IsNullOrWhiteSpace(text)) { error = "empty reply"; return null; }
                    return text.Trim();
                }
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return null;
            }
        }

        public static string ExtractOllamaText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(json);
            object message, content;
            var m = root != null && root.TryGetValue("message", out message) ? message as Dictionary<string, object> : null;
            return m != null && m.TryGetValue("content", out content) ? Convert.ToString(content) : null;
        }
        public static string Model => AppConfig.Get("Anthropic:Model", "claude-haiku-4-5-20251001");
        public static int HourlyLimit => Math.Max(1, AppConfig.GetInt("Ai:HourlyLimit", 20));

        public static readonly string[] StudentModes = { "explain", "practice", "plan" };
        public static readonly string[] TutorModes = { "explain", "practice", "tutorial" };

        public static string ModeLabel(string mode)
        {
            switch (mode)
            {
                case "practice": return "Practice questions";
                case "plan": return "Study plan";
                case "tutorial": return "Tutorial plan";
                default: return "Explain a concept";
            }
        }

        /// <summary>The instructions Claude follows. Only the module and year are shared; never names or student numbers.</summary>
        public static string SystemPrompt(string role, string mode, string moduleCode, string moduleName, int? yearOfStudy, DateTime today)
        {
            var sb = new StringBuilder();
            sb.Append("You are the TutorConnect study assistant at a South African university of technology. ");
            sb.Append("You are helping a ").Append(role == "Tutor" ? "student tutor who runs sessions and tutorials" : "student")
              .Append(" with the module ").Append(moduleCode).Append(" (").Append(moduleName).Append(")");
            if (yearOfStudy.HasValue) sb.Append(", year ").Append(yearOfStudy.Value);
            sb.Append(". Today is ").Append(today.ToString("dddd d MMMM yyyy")).Append(" (South African Standard Time). ");
            sb.Append("Use clear South African English, plain language and short paragraphs. Format with simple Markdown: headings, bullet lists, **bold** and code blocks where useful. ");
            sb.Append("Use South African examples (rand amounts written like R1 050,00, local businesses and places) when an example helps. ");
            sb.Append("Academic integrity matters: if asked to complete a graded assignment, test or exam answer word for word, explain the underlying ideas and guide the student to their own answer instead. ");
            sb.Append("If a question is outside this module, answer briefly and suggest asking their tutor or lecturer. If you're not sure of something, say so. ");
            sb.Append("Never ask for personal information. Keep answers under about 400 words unless a plan or question set needs more. ");

            switch (mode)
            {
                case "practice":
                    sb.Append("Task: write 5 practice questions on the topic the user gives, from easier to harder, mixing short-answer and applied questions. ");
                    sb.Append("Put the worked answers after a heading '## Answers' so the student can try first.");
                    break;
                case "plan":
                    sb.Append("Task: build a realistic, day-by-day study plan from the user's details (time available, test dates, weak topics). ");
                    sb.Append("Include short breaks, revision and a suggestion to book a session with their tutor for the hardest topic.");
                    break;
                case "tutorial":
                    sb.Append("Task: plan a tutorial session for the tutor: learning outcomes, a timed outline (for example 60 minutes), ");
                    sb.Append("an activity students do in pairs, 3 check-for-understanding questions, and common misconceptions to watch for.");
                    break;
                default:
                    sb.Append("Task: explain the concept step by step, starting from what a student at this level already knows, then give one worked example ");
                    sb.Append("and end with one quick question the student can use to check their understanding.");
                    break;
            }
            return sb.ToString();
        }

        /// <summary>Sends the conversation to Claude. Returns the reply, or null with a user-friendly error.</summary>
        public static string Ask(string systemPrompt, IList<Turn> conversation, out string error)
        {
            error = null;
            if (!ClaudeConfigured)
            {
                error = "The AI assistant isn't set up yet. An administrator needs to add a Claude API key (Anthropic:ApiKey) to Web.config.";
                return null;
            }

            var body = new Dictionary<string, object>
            {
                { "model", Model },
                { "max_tokens", AppConfig.GetInt("Ai:MaxTokens", 1200) },
                { "system", systemPrompt },
                { "messages", conversation.Select(t => new Dictionary<string, object> { { "role", t.Role }, { "content", t.Text } }).ToList() }
            };
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var payload = Encoding.UTF8.GetBytes(serializer.Serialize(body));

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                request.Headers["x-api-key"] = AppConfig.Get("Anthropic:ApiKey");
                request.Headers["anthropic-version"] = ApiVersion;
                using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var text = ExtractText(reader.ReadToEnd());
                    if (string.IsNullOrWhiteSpace(text)) error = "The assistant returned an empty answer. Try rephrasing your question.";
                    return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                var status = http == null ? 0 : (int)http.StatusCode;
                string detail = null;
                if (http != null)
                {
                    try { using (var r = new StreamReader(http.GetResponseStream())) detail = r.ReadToEnd(); } catch { }
                }
                AppLog.Write("AI", "Claude API error " + status + ": " + (detail ?? ex.Message));
                error = FriendlyError(status, ex);
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Error("AiService.Ask", ex);
                error = "The assistant couldn't answer right now. Please try again in a moment.";
                return null;
            }
        }

        public static string FriendlyError(int status, WebException ex)
        {
            switch (status)
            {
                case 400: return "The request couldn't be processed. Try a shorter question or start a new conversation.";
                case 401: return "The AI assistant's API key isn't valid. An administrator needs to check Anthropic:ApiKey in Web.config.";
                case 403: return "The API key doesn't have permission to use this model.";
                case 404: return "The configured AI model wasn't found. Check Anthropic:Model in Web.config.";
                case 429: return "The assistant is getting a lot of questions right now. Wait a minute and try again.";
                case 500:
                case 529: return "The AI service is busy. Please try again in a minute.";
                case 0: return ex != null && ex.Status == WebExceptionStatus.Timeout
                        ? "The assistant took too long to answer. Try a shorter question."
                        : "The server couldn't reach the AI service. Check the internet connection.";
                default: return "The AI service returned an error (" + status + "). Please try again.";
            }
        }

        /// <summary>Joins the text blocks of a Messages API response: { "content": [ { "type": "text", "text": "..." } ] }.</summary>
        public static string ExtractText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(json);
            object content;
            if (root == null || !root.TryGetValue("content", out content)) return null;
            var parts = new List<string>();
            foreach (var block in (content as System.Collections.IEnumerable) ?? new object[0])
            {
                var d = block as Dictionary<string, object>;
                object type, text;
                if (d != null && d.TryGetValue("type", out type) && Convert.ToString(type) == "text" && d.TryGetValue("text", out text))
                    parts.Add(Convert.ToString(text));
            }
            return string.Join("\n\n", parts);
        }

        /// <summary>Keeps the most recent turns so requests stay small; always starts with a user turn.</summary>
        public static List<Turn> Trim(List<Turn> history, int maxTurns = 10)
        {
            var list = history.Skip(Math.Max(0, history.Count - maxTurns)).ToList();
            while (list.Count > 0 && list[0].Role != "user") list.RemoveAt(0);
            return list;
        }
    }
}
