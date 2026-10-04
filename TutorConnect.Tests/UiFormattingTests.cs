using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Helpers;

namespace TutorConnect.Tests
{
    /// <summary>South African formatting: R1 050,00 (non-breaking space thousands, comma cents).</summary>
    [TestClass]
    public class UiFormattingTests
    {
        private const char Nbsp = '\u00A0';

        [TestMethod]
        public void Money_UsesSouthAfricanFormat()
        {
            Assert.AreEqual("R1" + Nbsp + "050,00", Ui.Money(1050m));
        }

        [TestMethod]
        public void Money_HourlyRate()
        {
            Assert.AreEqual("R105,00", Ui.Money(105m));
        }

        [TestMethod]
        public void Money_LargeAmountGroupsEveryThreeDigits()
        {
            Assert.AreEqual("R1" + Nbsp + "234" + Nbsp + "567,89", Ui.Money(1234567.89m));
        }

        [TestMethod]
        public void Money_Zero()
        {
            Assert.AreEqual("R0,00", Ui.Money(0m));
        }

        [TestMethod]
        public void Money_Negative()
        {
            Assert.AreEqual("-R210,00", Ui.Money(-210m));
        }

        [TestMethod]
        public void Initials_TakesFirstAndLastName()
        {
            Assert.AreEqual("LZ", Ui.Initials("Luthando Zulu"));
            Assert.AreEqual("SN", Ui.Initials("  sipho  mandla ndlovu "));
            Assert.AreEqual("?", Ui.Initials(""));
        }

        [TestMethod]
        public void Badge_UsesTrafficLightColours()
        {
            StringAssert.Contains(Ui.Badge("Present").ToHtmlString(), "badge-success");
            StringAssert.Contains(Ui.Badge("Late").ToHtmlString(), "badge-warning");
            StringAssert.Contains(Ui.Badge("Absent").ToHtmlString(), "badge-danger");
            StringAssert.Contains(Ui.Badge(null).ToHtmlString(), "Not marked");
        }

        [TestMethod]
        public void Badge_EncodesUnexpectedText()
        {
            StringAssert.Contains(Ui.Badge("<script>").ToHtmlString(), "&lt;script&gt;");
        }

        [TestMethod]
        public void Pct_HandlesZeroTotal()
        {
            Assert.AreEqual(0, Ui.Pct(3, 0));
            Assert.AreEqual(67, Ui.Pct(2, 3));
        }

        [TestMethod]
        public void RateTone_Thresholds()
        {
            Assert.AreEqual("success", Ui.RateTone(75m));
            Assert.AreEqual("warning", Ui.RateTone(50m));
            Assert.AreEqual("danger", Ui.RateTone(49.9m));
        }
    }
}
