using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TutorConnect.Services
{
    /// <summary>
    /// The assistant's last fallback when neither Claude nor a local AI (Ollama) is available.
    /// It doesn't pretend to be an AI: it builds structured, genuinely useful study material from the
    /// student's own words (a guided method, practice questions, a dated plan, a tutorial outline).
    /// </summary>
    public static class OfflineStudyHelper
    {
        public const string Notice = "*Offline study helper. Full AI answers aren't available on this server right now, so here is a structured way to work through it.*";

        public static string Reply(string mode, string moduleCode, string moduleName, string message, DateTime today)
        {
            var topic = Topic(message);
            switch (mode)
            {
                case "practice": return Practice(moduleCode, topic);
                case "plan": return Plan(moduleCode, moduleName, message, today);
                case "tutorial": return Tutorial(moduleCode, topic);
                default: return Explain(moduleCode, moduleName, topic);
            }
        }

        /// <summary>Turns "Can you please explain recursion to me?" into "recursion".</summary>
        public static string Topic(string message)
        {
            var t = (message ?? "").Trim().TrimEnd('?', '.', '!');
            t = Regex.Replace(t, @"^(hi|hello|hey)[,!\s]+", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^(can|could|would) you( please)?\s+", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^(please\s+)?(explain|describe|define|tell me about|what is|what are|what's|how does|how do|help me (with|understand)|give me|make me|create|write|generate)\s+", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^(an?|the|some|five|5|ten|10)\s+", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^(practice |practise )?(questions?|quiz(zes)?)\s+(on|about|for)\s+", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s+(to me|for me|in simple terms|simply|please)$", "", RegexOptions.IgnoreCase);
            t = t.Trim();
            if (t.Length == 0) t = "this topic";
            return t.Length > 80 ? t.Substring(0, 80).TrimEnd() + "…" : t;
        }

        private static string Explain(string code, string name, string topic)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Notice).AppendLine();
            sb.AppendLine("### Working through: " + topic).AppendLine();
            sb.AppendLine("1. **Find it in your " + code + " material.** Look up \"" + topic + "\" in your " + name + " notes and slides, and note the exact definition your lecturer uses.");
            sb.AppendLine("2. **Say it in one sentence.** Explain " + topic + " as if to a friend who hasn't done " + code + ". If you can't, that's the part to revisit.");
            sb.AppendLine("3. **Break it into parts.** List the steps, rules or components, and how each connects to the next.");
            sb.AppendLine("4. **Work one example.** Use something local, for example a spaza shop's stock system, a taxi rank queue or a student's fee account.");
            sb.AppendLine("5. **Find the edges.** When does " + topic + " *not* apply? What's the most common mistake with it?").AppendLine();
            sb.AppendLine("**Check yourself:** close your notes and write down, in two minutes, what " + topic + " is and one place it's used.").AppendLine();
            sb.AppendLine("Still stuck? Book a one-on-one session with your " + code + " tutor under **Find a tutor**.");
            return sb.ToString();
        }

        private static string Practice(string code, string topic)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Notice).AppendLine();
            sb.AppendLine("### Practice questions: " + topic).AppendLine();
            sb.AppendLine("1. **Define it.** In your own words, what is " + topic + "? Give one reason it matters in " + code + ".");
            sb.AppendLine("2. **Identify it.** Describe a situation where you would use " + topic + ", and one where you wouldn't.");
            sb.AppendLine("3. **Apply it.** A small business in Durban wants to use " + topic + ". Walk through the steps they would follow.");
            sb.AppendLine("4. **Spot the error.** Write down a common mistake students make with " + topic + " and explain how to fix it.");
            sb.AppendLine("5. **Compare it.** How is " + topic + " different from a related idea in " + code + "? Give one advantage of each.").AppendLine();
            sb.AppendLine("## Answers").AppendLine();
            sb.AppendLine("Mark yourself against your lecture notes: a full answer uses the correct terms, gives an example, and explains *why*, not just *what*. Take any question you couldn't answer to your next tutorial or session.");
            return sb.ToString();
        }

        private static string Plan(string code, string name, string message, DateTime today)
        {
            var days = 7;
            var m = Regex.Match(message ?? "", @"(\d{1,2})\s*-?\s*day", RegexOptions.IgnoreCase);
            if (m.Success) days = Math.Max(2, Math.Min(21, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));
            var hours = 2;
            var h = Regex.Match(message ?? "", @"(\d)\s*(hours?|hrs?|h)\b", RegexOptions.IgnoreCase);
            if (h.Success) hours = Math.Max(1, Math.Min(8, int.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture)));

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(Notice).AppendLine();
            sb.AppendLine("### " + days + "-day study plan for " + code + " (" + hours + (hours == 1 ? " hour" : " hours") + " a day)").AppendLine();
            for (var i = 0; i < days; i++)
            {
                var d = today.AddDays(i + 1);
                string task;
                if (i == days - 1) task = "Light review only: skim your summary sheet, sleep early.";
                else if (i == days - 2) task = "Full practice paper under timed conditions, then mark it.";
                else if (i == 0) task = "List every topic in " + name + " and rate yourself 1 to 5 on each.";
                else if (i == 1) task = "Start with your two weakest topics: rewrite the notes in your own words.";
                else if (i == days / 2) task = "Book a session with your " + code + " tutor for the hardest topic, and prepare 3 questions.";
                else if (i % 2 == 0) task = "Practice questions on the topics you rated 3 or lower.";
                else task = "Next weakest topic: notes, one worked example, then 3 practice questions.";
                sb.AppendLine("- **" + d.ToString("ddd d MMM", ci) + ":** " + task);
            }
            sb.AppendLine().AppendLine("Work in 25-minute blocks with 5-minute breaks. Adjust the plan by typing, for example, \"10-day plan, 3 hours a day\".");
            return sb.ToString();
        }

        private static string Tutorial(string code, string topic)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Notice).AppendLine();
            sb.AppendLine("### 60-minute tutorial: " + topic + " (" + code + ")").AppendLine();
            sb.AppendLine("**Learning outcomes:** by the end, students can explain " + topic + ", apply it to an example, and avoid the most common mistake.").AppendLine();
            sb.AppendLine("- **0 to 5 min:** check-in by QR code; one warm-up question on the board.");
            sb.AppendLine("- **5 to 15 min:** short recap of " + topic + " with one worked example.");
            sb.AppendLine("- **15 to 35 min:** pairs work through two problems; you circulate and note common errors.");
            sb.AppendLine("- **35 to 50 min:** pairs present one solution each; discuss the errors you noted.");
            sb.AppendLine("- **50 to 60 min:** three quick check-for-understanding questions; post the answers as an announcement afterwards.").AppendLine();
            sb.AppendLine("**Watch for:** students memorising steps without knowing *why*; ask \"what would change if…?\"");
            return sb.ToString();
        }
    }
}
