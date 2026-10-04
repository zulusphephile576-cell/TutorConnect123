using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    [TestClass]
    public class PaymentAndModuleTests
    {
        [TestMethod]
        public void AccountNumber_AcceptsSpacesAndDashes()
        {
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber("1234 5678 90"));
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber("62-1234-5678"));
            Assert.AreEqual("1234567890", SouthAfricanBanks.Digits("1234 5678 90"));
        }

        [TestMethod]
        public void AccountNumber_RejectsLettersAndWrongLengths()
        {
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber("12345"));
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber("12345678901234567"));
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber("1234abcd90"));
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber(""));
        }

        [TestMethod]
        public void Last4_IsAllThatIsKept()
        {
            Assert.AreEqual("7890", SouthAfricanBanks.Last4("1234 5678 90"));
        }

        [TestMethod]
        public void Banks_HaveUniversalBranchCodes()
        {
            Assert.AreEqual(10, SouthAfricanBanks.All.Count);
            Assert.AreEqual("470010", SouthAfricanBanks.Find("capitec").BranchCode);
            Assert.AreEqual("250655", SouthAfricanBanks.Find("fnb").BranchCode);
            Assert.IsTrue(SouthAfricanBanks.All.All(b => b.BranchCode.Length == 6));
            Assert.IsNull(SouthAfricanBanks.Find("unknown"));
        }

        [TestMethod]
        public void AccountLength_MatchesTheBank()
        {
            var capitec = SouthAfricanBanks.Find("capitec");
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber(capitec, "1234 5678 90"));      // 10
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber(capitec, "123456789"));        // 9
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber(capitec, "1234567890123"));    // 13
            var fnb = SouthAfricanBanks.Find("fnb");
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber(fnb, "62012345678"));           // 11
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber(fnb, "6201234567"));           // 10
        }

        [TestMethod]
        public void AccountLength_StandardBankAllowsOldAndNewNumbers()
        {
            var sb = SouthAfricanBanks.Find("standard");
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber(sb, "123456789"));      // old, 9
            Assert.IsTrue(SouthAfricanBanks.IsValidAccountNumber(sb, "10123456789"));    // new, 11
            Assert.IsFalse(SouthAfricanBanks.IsValidAccountNumber(sb, "1012345678"));    // 10
            Assert.AreEqual("9 or 11 digits", sb.LengthText);
            Assert.AreEqual("8 to 11 digits", SouthAfricanBanks.Find("absa").LengthText);
            Assert.AreEqual("10 digits", SouthAfricanBanks.Find("nedbank").LengthText);
        }

        [TestMethod]
        public void AccountProblem_SaysExactlyWhatIsWrong()
        {
            Assert.AreEqual("Capitec Bank account numbers are 10 digits. You entered 13.",
                SouthAfricanBanks.AccountNumberProblem(SouthAfricanBanks.Find("capitec"), "1234567890123"));
            Assert.IsNull(SouthAfricanBanks.AccountNumberProblem(SouthAfricanBanks.Find("capitec"), "1234567890"));
            Assert.AreEqual("Account numbers contain digits only.", SouthAfricanBanks.AccountNumberProblem(SouthAfricanBanks.Find("nedbank"), "12ab"));
        }

        [TestMethod]
        public void FindByName_RecognisesOlderSavedNames()
        {
            Assert.AreEqual("absa", SouthAfricanBanks.FindByName("ABSA").Code);
            Assert.AreEqual("capitec", SouthAfricanBanks.FindByName("Capitec").Code);
            Assert.AreEqual("fnb", SouthAfricanBanks.FindByName("FNB").Code);
            Assert.AreEqual("standard", SouthAfricanBanks.FindByName("Standard Bank").Code);
            Assert.IsNull(SouthAfricanBanks.FindByName("Other"));
        }

        private static string Card(string holder = "L Zulu", string brand = "Visa", string last4 = "1111", string expiry = "12 / 30",
            string street = "41 ML Sultan Road", string city = "Durban", string postal = "4001", string country = "South Africa")
        {
            return TutorConnect.Controllers.PaymentController.CardProblem(holder, brand, last4, expiry, street, city, postal, country);
        }

        [TestMethod]
        public void CardProblem_AcceptsAValidCard()
        {
            Assert.IsNull(Card());
            Assert.IsNull(Card(brand: "American Express"));
        }

        [TestMethod]
        public void CardProblem_RejectsBadDetails()
        {
            StringAssert.Contains(Card(brand: "Diners"), "Visa, Mastercard or American Express");
            StringAssert.Contains(Card(last4: "12"), "Visa, Mastercard or American Express");
            StringAssert.Contains(Card(expiry: "13 / 30"), "between 01 and 12");
            StringAssert.Contains(Card(expiry: "01 / 20"), "expired");
            StringAssert.Contains(Card(expiry: "1230"), "MM / YY");
            StringAssert.Contains(Card(postal: "400"), "4 digits");
            StringAssert.Contains(Card(holder: " "), "cardholder");
            StringAssert.Contains(Card(country: "Atlantis"), "country");
        }

        [TestMethod]
        public void CvMustBePdfOrWord()
        {
            CollectionAssert.Contains(FileStorage.CvExtensions, ".pdf");
            CollectionAssert.Contains(FileStorage.CvExtensions, ".docx");
            CollectionAssert.DoesNotContain(FileStorage.CvExtensions, ".jpg");
        }

        [TestMethod]
        public void ModuleRow_BlankRowsAreIgnored()
        {
            Assert.IsTrue(new ModuleRow().IsBlank);
            Assert.IsTrue(new ModuleRow { Code = "  ", Name = "" }.IsBlank);
            Assert.IsFalse(new ModuleRow { Code = "ITDB201" }.IsBlank);
            Assert.AreEqual(1, new ModuleRow().Semester, "first semester by default");
        }
    }
}
