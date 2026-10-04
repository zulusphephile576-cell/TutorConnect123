using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>The AI study assistant's request building and response parsing (no network calls).</summary>
    [TestClass]
    public class AiServiceTests
    {
        [TestMethod]
        public void ExtractText_ReadsMessagesApiContentBlocks()
        {
            var json = "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":\"Recursion is a function calling itself.\"}],\"stop_reason\":\"end_turn\"}";
            Assert.AreEqual("Recursion is a function calling itself.", AiService.ExtractText(json));
        }

        [TestMethod]
        public void ExtractText_JoinsTextBlocksAndSkipsOthers()
        {
            var json = "{\"content\":[{\"type\":\"text\",\"text\":\"Part one.\"},{\"type\":\"tool_use\",\"id\":\"x\"},{\"type\":\"text\",\"text\":\"Part two.\"}]}";
            Assert.AreEqual("Part one.\n\nPart two.", AiService.ExtractText(json));
        }

        [TestMethod]
        public void ExtractText_HandlesEmptyOrUnexpectedJson()
        {
            Assert.IsNull(AiService.ExtractText(""));
            Assert.IsNull(AiService.ExtractText("{\"type\":\"error\"}"));
        }

        [TestMethod]
        public void Trim_KeepsRecentTurnsStartingWithUser()
        {
            var history = new List<AiService.Turn>();
            for (var i = 0; i < 15; i++)
                history.Add(new AiService.Turn { Role = i % 2 == 0 ? "user" : "assistant", Text = "t" + i });
            var trimmed = AiService.Trim(history, 10);
            Assert.IsTrue(trimmed.Count <= 10);
            Assert.AreEqual("user", trimmed[0].Role);
            Assert.AreEqual("t14", trimmed[trimmed.Count - 1].Text);
        }

        [TestMethod]
        public void SystemPrompt_IncludesModuleYearAndSouthAfricanContext()
        {
            var p = AiService.SystemPrompt("Student", "explain", "ITPR101", "Programming 1", 1, new DateTime(2026, 9, 28));
            StringAssert.Contains(p, "ITPR101");
            StringAssert.Contains(p, "year 1");
            StringAssert.Contains(p, "South African");
            StringAssert.Contains(p, "Monday 28 September 2026");
        }

        [TestMethod]
        public void SystemPrompt_ProtectsAcademicIntegrity()
        {
            var p = AiService.SystemPrompt("Student", "explain", "ITDB101", "Databases 1", 1, DateTime.Today);
            StringAssert.Contains(p, "Academic integrity");
        }

        [TestMethod]
        public void SystemPrompt_ModesAskForDifferentOutput()
        {
            StringAssert.Contains(AiService.SystemPrompt("Student", "practice", "X", "Y", 1, DateTime.Today), "## Answers");
            StringAssert.Contains(AiService.SystemPrompt("Student", "plan", "X", "Y", 1, DateTime.Today), "study plan");
            StringAssert.Contains(AiService.SystemPrompt("Tutor", "tutorial", "X", "Y", 1, DateTime.Today), "tutorial session");
        }

        [TestMethod]
        public void FriendlyError_ExplainsCommonFailures()
        {
            StringAssert.Contains(AiService.FriendlyError(401, null), "API key");
            StringAssert.Contains(AiService.FriendlyError(429, null), "Wait a minute");
            StringAssert.Contains(AiService.FriendlyError(0, new WebException("t", WebExceptionStatus.Timeout)), "too long");
            StringAssert.Contains(AiService.FriendlyError(0, new WebException("d", WebExceptionStatus.NameResolutionFailure)), "internet");
        }
    }
}
