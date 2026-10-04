using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    [TestClass]
    public class RegistrationRulesTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 27);

        private static RegisterViewModel Valid()
        {
            return new RegisterViewModel
            {
                FirstName = "Luthando", LastName = "Zulu", DateOfBirth = new DateTime(2004, 5, 12), Phone = "071 234 5678",
                Email = "22012345@dut4life.ac.za", StudentNumber = "22012345", CourseId = 1, YearOfStudy = 2,
                Password = "Secret@123", ConfirmPassword = "Secret@123", AcceptTerms = true, AcceptPrivacy = true
            };
        }

        private static bool HasError(RegisterViewModel m, string field)
        {
            return RegistrationRules.Validate(m, Today).Any(e => e.Key == field);
        }

        [TestMethod]
        public void ValidStudent_PassesAllRules()
        {
            Assert.AreEqual(0, RegistrationRules.Validate(Valid(), Today).Count);
        }

        [TestMethod]
        public void Privacy_ConsentIsRequired()
        {
            var m = Valid(); m.AcceptPrivacy = false;
            Assert.IsTrue(HasError(m, "AcceptPrivacy"));
        }

        [TestMethod]
        public void Terms_ConfirmationIsRequired()
        {
            var m = Valid(); m.AcceptTerms = false;
            Assert.IsTrue(HasError(m, "AcceptTerms"));
        }

        [TestMethod]
        public void DateOfBirth_TooYoungIsRejected()
        {
            var m = Valid(); m.DateOfBirth = Today.AddYears(-10);
            Assert.IsTrue(HasError(m, "DateOfBirth"));
        }

        [TestMethod]
        public void DateOfBirth_InTheFutureIsRejected()
        {
            var m = Valid(); m.DateOfBirth = Today.AddDays(1);
            Assert.IsTrue(HasError(m, "DateOfBirth"));
        }

        [TestMethod]
        public void AgeOn_CountsBirthdayCorrectly()
        {
            Assert.AreEqual(21, RegistrationRules.AgeOn(new DateTime(2005, 9, 27), Today));
            Assert.AreEqual(20, RegistrationRules.AgeOn(new DateTime(2005, 9, 28), Today));
        }

        [TestMethod]
        public void EmergencyContact_NeedsNameAndNumberTogether()
        {
            var m = Valid(); m.EmergencyContactName = "Nomsa Zulu";
            Assert.IsTrue(HasError(m, "EmergencyContactPhone"));
            m.EmergencyContactPhone = "082 555 0101";
            Assert.IsFalse(HasError(m, "EmergencyContactPhone"));
        }

        [TestMethod]
        public void Gender_MustComeFromTheList()
        {
            var m = Valid(); m.Gender = "Something else";
            Assert.IsTrue(HasError(m, "Gender"));
        }

        [TestMethod]
        public void SouthAfricanPhone_AcceptsLocalAndInternationalFormats()
        {
            Assert.IsTrue(RegistrationRules.IsSouthAfricanPhone("071 234 5678"));
            Assert.IsTrue(RegistrationRules.IsSouthAfricanPhone("+27 71 234 5678"));
            Assert.IsTrue(RegistrationRules.IsSouthAfricanPhone("(031) 373-2000"));
        }

        [TestMethod]
        public void SouthAfricanPhone_RejectsInvalidNumbers()
        {
            Assert.IsFalse(RegistrationRules.IsSouthAfricanPhone("12345"));
            Assert.IsFalse(RegistrationRules.IsSouthAfricanPhone("0012345678"));
            Assert.IsFalse(RegistrationRules.IsSouthAfricanPhone("+44 20 7946 0958"));
            Assert.IsFalse(RegistrationRules.IsSouthAfricanPhone(""));
        }
    }
}
