using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>The assistant must always answer: Claude, then a local Ollama model, then the offline helper.</summary>
    [TestClass]
    public class AiFallbackTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 9, 28);

        [TestMethod]
        public void WithNoKeyAndNoLocalAi_StillAnswersOffline()
        {
            AiService.Provider used;
            var conversation = new List<AiService.Turn> { new AiService.Turn { Role = "user", Text = "Explain recursion" } };
            var answer = AiService.AskWithFallback("system", conversation, "explain", "ITPR101", "Programming 1", Monday, out used);
            Assert.IsFalse(string.IsNullOrWhiteSpace(answer));
            Assert.AreEqual("offline", used.Key);
            StringAssert.Contains(answer, "recursion");
        }

        [TestMethod]
        public void Topic_StripsFillerWords()
        {
            Assert.AreEqual("recursion", OfflineStudyHelper.Topic("Can you please explain recursion to me?"));
            Assert.AreEqual("SQL joins", OfflineStudyHelper.Topic("Give me five practice questions on SQL joins"));
            Assert.AreEqual("normalisation", OfflineStudyHelper.Topic("What is normalisation"));
            Assert.AreEqual("this topic", OfflineStudyHelper.Topic("   "));
        }

        [TestMethod]
        public void Practice_HasFiveQuestionsAndAnswersSection()
        {
            var r = OfflineStudyHelper.Reply("practice", "ITDB101", "Databases 1", "questions on SQL joins", Monday);
            StringAssert.Contains(r, "5. ");
            StringAssert.Contains(r, "## Answers");
            StringAssert.Contains(r, OfflineStudyHelper.Notice);
        }

        [TestMethod]
        public void Plan_UsesRequestedDaysAndRealDates()
        {
            var r = OfflineStudyHelper.Reply("plan", "ITPR101", "Programming 1", "10-day plan, 3 hours a day", Monday);
            StringAssert.Contains(r, "10-day study plan");
            StringAssert.Contains(r, "3 hours a day");
            StringAssert.Contains(r, "Tue 29 Sep");        // starts the day after today (SAST)
            StringAssert.Contains(r, "Thu 8 Oct");         // tenth day
        }

        [TestMethod]
        public void Plan_DefaultsToSevenDays()
        {
            StringAssert.Contains(OfflineStudyHelper.Reply("plan", "X", "Y", "help me plan", Monday), "7-day study plan");
        }

        [TestMethod]
        public void Tutorial_IsSixtyMinuteOutline()
        {
            var r = OfflineStudyHelper.Reply("tutorial", "ITPR101", "Programming 1", "loops", Monday);
            StringAssert.Contains(r, "60-minute tutorial: loops");
            StringAssert.Contains(r, "50 to 60 min");
        }

        [TestMethod]
        public void PickOllamaModel_PrefersConfiguredModel()
        {
            var tags = "{\"models\":[{\"name\":\"qwen2.5:3b\"},{\"name\":\"llama3.2:latest\"}]}";
            Assert.AreEqual("llama3.2:latest", AiService.PickOllamaModel(tags, "llama3.2"));
        }

        [TestMethod]
        public void PickOllamaModel_FallsBackToFirstInstalled()
        {
            Assert.AreEqual("qwen2.5:3b", AiService.PickOllamaModel("{\"models\":[{\"name\":\"qwen2.5:3b\"}]}", "llama3.2"));
            Assert.IsNull(AiService.PickOllamaModel("{\"models\":[]}", "llama3.2"));
            Assert.IsNull(AiService.PickOllamaModel("", "llama3.2"));
        }

        [TestMethod]
        public void ExtractOllamaText_ReadsChatReply()
        {
            var json = "{\"model\":\"llama3.2\",\"message\":{\"role\":\"assistant\",\"content\":\"A loop repeats code.\"},\"done\":true}";
            Assert.AreEqual("A loop repeats code.", AiService.ExtractOllamaText(json));
            Assert.IsNull(AiService.ExtractOllamaText("{\"error\":\"model not found\"}"));
        }
    }
}
