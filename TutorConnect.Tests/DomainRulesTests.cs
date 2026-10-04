using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Helpers;
using TutorConnect.Models;

namespace TutorConnect.Tests
{
    /// <summary>Small rules on the entities that drive what the pages show.</summary>
    [TestClass]
    public class DomainRulesTests
    {
        [TestMethod]
        public void Slot_MaxHoursIsWholeHoursAndAtLeastOne()
        {
            var s = new Slot { StartTime = new DateTime(2026, 10, 1, 14, 0, 0), EndTime = new DateTime(2026, 10, 1, 16, 45, 0) };
            Assert.AreEqual(2, s.MaxHours);
            s.EndTime = s.StartTime.AddMinutes(30);
            Assert.AreEqual(1, s.MaxHours);
        }

        [TestMethod]
        public void Tutorial_IsLiveOnlyWhileRunning()
        {
            var now = Clock.Now;
            Assert.IsFalse(new Tutorial { StartTime = now.AddMinutes(20), EndTime = now.AddMinutes(80) }.IsLive, "not before it starts");
            Assert.IsTrue(new Tutorial { StartTime = now.AddMinutes(-5), EndTime = now.AddMinutes(55) }.IsLive, "open once started");
            Assert.IsFalse(new Tutorial { StartTime = now.AddMinutes(-90), EndTime = now.AddMinutes(-30) }.IsLive, "closed after it ends");
            Assert.IsTrue(new Tutorial { StartTime = now.AddHours(-3), EndTime = now.AddHours(-1) }.IsPast);
        }

        [TestMethod]
        public void Tutorial_MarkedCountAddsAllStatuses()
        {
            Assert.AreEqual(6, new Tutorial { PresentCount = 3, LateCount = 2, AbsentCount = 1 }.MarkedCount);
        }

        [TestMethod]
        public void User_LockoutExpires()
        {
            Assert.IsTrue(new User { LockoutUntil = Clock.Now.AddMinutes(10) }.IsLockedOut);
            Assert.IsFalse(new User { LockoutUntil = Clock.Now.AddMinutes(-1) }.IsLockedOut);
            Assert.IsFalse(new User().IsLockedOut);
        }

        [TestMethod]
        public void Application_IsDecidedOnlyWhenApprovedOrRejected()
        {
            Assert.IsTrue(new TutorApplication { Status = "Approved" }.IsDecided);
            Assert.IsTrue(new TutorApplication { Status = "Rejected" }.IsDecided);
            Assert.IsFalse(new TutorApplication { Status = "Recommended" }.IsDecided);
        }

        [TestMethod]
        public void ApiToken_IsActiveUntilRevokedOrExpired()
        {
            Assert.IsTrue(new ApiTokenInfo { ExpiresAt = Clock.Now.AddDays(1) }.IsActive);
            Assert.IsFalse(new ApiTokenInfo { ExpiresAt = Clock.Now.AddDays(1), RevokedAt = Clock.Now }.IsActive);
            Assert.IsFalse(new ApiTokenInfo { ExpiresAt = Clock.Now.AddSeconds(-1) }.IsActive);
        }
    }
}
