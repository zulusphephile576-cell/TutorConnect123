using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Helpers;
using TutorConnect.Models;

namespace TutorConnect.Tests
{
    [TestClass]
    public class ClockTests
    {
        [TestMethod]
        public void Now_IsSouthAfricanStandardTime()
        {
            var expected = DateTime.UtcNow.AddHours(2);
            Assert.IsTrue(Math.Abs((Clock.Now - expected).TotalSeconds) < 5, "SAST is UTC+2 with no daylight saving");
        }

        [TestMethod]
        public void Iso_IncludesSastOffset()
        {
            Assert.AreEqual("2026-09-27T14:30:00+02:00", Clock.Iso(new DateTime(2026, 9, 27, 14, 30, 0)));
            Assert.IsNull(Clock.Iso((DateTime?)null));
        }

        [TestMethod]
        public void MondayOf_ReturnsStartOfWeek()
        {
            Assert.AreEqual(new DateTime(2026, 9, 21), ScheduleViewModel.MondayOf(new DateTime(2026, 9, 27)));   // Sunday
            Assert.AreEqual(new DateTime(2026, 9, 21), ScheduleViewModel.MondayOf(new DateTime(2026, 9, 21, 18, 0, 0)));
            Assert.AreEqual(new DateTime(2026, 9, 28), ScheduleViewModel.MondayOf(new DateTime(2026, 10, 1)));
        }
    }
}
