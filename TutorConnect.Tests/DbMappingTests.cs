using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Data;
using TutorConnect.Models;

namespace TutorConnect.Tests
{
    /// <summary>Row-to-object mapping used for every stored procedure result (no database needed).</summary>
    [TestClass]
    public class DbMappingTests
    {
        [TestMethod]
        public void Map_CopiesMatchingColumns()
        {
            var t = new DataTable();
            t.Columns.Add("BookingId", typeof(int));
            t.Columns.Add("ModuleCode", typeof(string));
            t.Columns.Add("Amount", typeof(decimal));
            t.Columns.Add("StartTime", typeof(DateTime));
            t.Columns.Add("NotAProperty", typeof(string));
            t.Rows.Add(7, "ITPR101", 210m, new DateTime(2026, 9, 29, 14, 0, 0), "ignored");

            var b = Db.Map<Booking>(t)[0];
            Assert.AreEqual(7, b.BookingId);
            Assert.AreEqual("ITPR101", b.ModuleCode);
            Assert.AreEqual(210m, b.Amount);
            Assert.AreEqual(14, b.StartTime.Hour);
        }

        [TestMethod]
        public void Map_LeavesNullColumnsAtDefault()
        {
            var t = new DataTable();
            t.Columns.Add("AttendanceStatus", typeof(string));
            t.Columns.Add("PayoutId", typeof(int));
            t.Rows.Add(DBNull.Value, DBNull.Value);
            Assert.IsNull(Db.Map<Booking>(t)[0].AttendanceStatus);
            Assert.IsNull(Db.Map<Payment>(t)[0].PayoutId);
        }

        [TestMethod]
        public void Map_ConvertsCompatibleTypes()
        {
            var t = new DataTable();
            t.Columns.Add("Hours", typeof(long));        // e.g. SUM() returns a wider type
            t.Columns.Add("Rate", typeof(double));
            t.Rows.Add(3L, 87.5d);
            Assert.AreEqual(3, Db.Map<TutorActivity>(t)[0].Hours);
            Assert.AreEqual(87.5m, Db.Map<StudentProgress>(t)[0].Rate);
        }

        [TestMethod]
        public void Map_FillsNullableProperties()
        {
            var t = new DataTable();
            t.Columns.Add("YearOfStudy", typeof(int));
            t.Rows.Add(2);
            Assert.AreEqual(2, Db.Map<User>(t)[0].YearOfStudy);
        }

        [TestMethod]
        public void Map_EmptyOrNullTableGivesEmptyList()
        {
            Assert.AreEqual(0, Db.Map<User>(new DataTable()).Count);
            Assert.AreEqual(0, Db.Map<User>(null).Count);
        }

        [TestMethod]
        public void Row_IsCaseInsensitive()
        {
            var t = new DataTable();
            t.Columns.Add("PendingRequests", typeof(int));
            t.Rows.Add(4);
            var row = Db.Row(t);
            Assert.AreEqual(4, row["pendingrequests"]);
        }

        [TestMethod]
        public void Stat_ReadsDashboardValues()
        {
            var t = new DataTable();
            t.Columns.Add("UpcomingSessions", typeof(int));
            t.Columns.Add("AttendanceRate", typeof(decimal));
            t.Rows.Add(3, 85.7m);
            var vm = new DashboardViewModel { Stats = Db.Row(t) };
            Assert.AreEqual(3, vm.Stat("UpcomingSessions"));
            Assert.AreEqual(85.7m, vm.StatDecimal("AttendanceRate"));
            Assert.AreEqual(0, vm.Stat("Missing"));
        }
    }
}
