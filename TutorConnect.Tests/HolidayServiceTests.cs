using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>South African public holidays (Public Holidays Act 36 of 1994) and parsing of the Nager.Date API.</summary>
    [TestClass]
    public class HolidayServiceTests
    {
        [TestMethod]
        public void EasterSunday_KnownYears()
        {
            Assert.AreEqual(new DateTime(2024, 3, 31), HolidayService.EasterSunday(2024));
            Assert.AreEqual(new DateTime(2025, 4, 20), HolidayService.EasterSunday(2025));
            Assert.AreEqual(new DateTime(2026, 4, 5), HolidayService.EasterSunday(2026));
            Assert.AreEqual(new DateTime(2027, 3, 28), HolidayService.EasterSunday(2027));
        }

        [TestMethod]
        public void BuiltIn_2026_HasEasterHolidays()
        {
            var h = HolidayService.BuiltIn(2026);
            Assert.IsTrue(h.Any(x => x.Date == new DateTime(2026, 4, 3) && x.Name == "Good Friday"));
            Assert.IsTrue(h.Any(x => x.Date == new DateTime(2026, 4, 6) && x.Name == "Family Day"));
        }

        [TestMethod]
        public void BuiltIn_SundayHolidayMovesToMonday()
        {
            // National Women's Day, 9 August 2026, is a Sunday.
            Assert.IsTrue(HolidayService.BuiltIn(2026).Any(x => x.Date == new DateTime(2026, 8, 10)));
            // Human Rights Day, 21 March 2027, is a Sunday.
            Assert.IsTrue(HolidayService.BuiltIn(2027).Any(x => x.Date == new DateTime(2027, 3, 22)));
        }

        [TestMethod]
        public void BuiltIn_HasTwelveStatutoryHolidaysPlusObserved()
        {
            Assert.AreEqual(13, HolidayService.BuiltIn(2026).Count);   // 12 + Women's Day observed on Monday
        }

        [TestMethod]
        public void ParseNager_ReadsTheApiFormat()
        {
            var json = "[{\"date\":\"2026-12-16\",\"localName\":\"Day of Reconciliation\",\"name\":\"Day of Reconciliation\",\"countryCode\":\"ZA\"}," +
                       "{\"date\":\"2026-01-01\",\"localName\":\"New Year's Day\",\"name\":\"New Year's Day\",\"countryCode\":\"ZA\"}]";
            var list = HolidayService.ParseNager(json);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(new DateTime(2026, 1, 1), list[0].Date, "Sorted by date");
            Assert.AreEqual("Day of Reconciliation", list[1].Name);
        }

        [TestMethod]
        public void ParseNager_IgnoresBadRowsAndEmptyInput()
        {
            Assert.AreEqual(0, HolidayService.ParseNager("").Count);
            Assert.AreEqual(1, HolidayService.ParseNager("[{\"date\":\"not a date\",\"name\":\"x\"},{\"date\":\"2026-04-27\",\"name\":\"Freedom Day\"}]").Count);
        }
    }
}
