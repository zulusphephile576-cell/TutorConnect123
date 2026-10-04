using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod, TestCategory("BCrypt")]
        public void Hash_VerifiesCorrectPasswordOnly()
        {
            var hash = PasswordHasher.Hash("Password@123");
            Assert.IsTrue(PasswordHasher.Verify("Password@123", hash));
            Assert.IsFalse(PasswordHasher.Verify("password@123", hash));
        }

        [TestMethod, TestCategory("BCrypt")]
        public void Hash_IsSaltedAndNeverPlainText()
        {
            var a = PasswordHasher.Hash("Password@123");
            var b = PasswordHasher.Hash("Password@123");
            Assert.AreNotEqual(a, b);
            Assert.IsFalse(a.Contains("Password@123"));
            StringAssert.StartsWith(a, "$2");
        }

        [TestMethod]
        public void Verify_HandlesEmptyAndCorruptInput()
        {
            Assert.IsFalse(PasswordHasher.Verify("", "$2a$11$abc"));
            Assert.IsFalse(PasswordHasher.Verify("x", null));
        }

        [TestMethod]
        public void GenerateTemporary_IsReadableAndStrong()
        {
            var p = PasswordHasher.GenerateTemporary();
            Assert.IsTrue(Regex.IsMatch(p, "^Tc-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}!$"), p);
            Assert.AreNotEqual(p, PasswordHasher.GenerateTemporary());
        }

        [TestMethod]
        public void NewToken_IsUrlSafe()
        {
            var t = PasswordHasher.NewToken();
            Assert.IsTrue(Regex.IsMatch(t, "^[A-Za-z0-9_-]{40,}$"), t);
        }
    }
}
