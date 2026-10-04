using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    [TestClass]
    public class ApiTokenServiceTests
    {
        [TestMethod]
        public void Generate_HasPrefixAndLength()
        {
            var t = ApiTokenService.Generate();
            StringAssert.StartsWith(t, "tc_");
            Assert.AreEqual(43, t.Length);
            Assert.IsTrue(ApiTokenService.LooksValid(t));
        }

        [TestMethod]
        public void Generate_IsRandom()
        {
            var tokens = Enumerable.Range(0, 200).Select(i => ApiTokenService.Generate()).ToList();
            Assert.AreEqual(200, tokens.Distinct().Count());
        }

        [TestMethod]
        public void Hash_IsSha256Hex()
        {
            Assert.AreEqual("90fff5569ca1f96ae47e6d88eddfedde025a740d7ebc6d0c284610005c6ece44", ApiTokenService.Hash("tc_example"));
        }

        [TestMethod]
        public void Hash_NeverReturnsTheToken()
        {
            var t = ApiTokenService.Generate();
            Assert.AreNotEqual(t, ApiTokenService.Hash(t));
            Assert.AreEqual(64, ApiTokenService.Hash(t).Length);
        }

        [TestMethod]
        public void LooksValid_RejectsMalformedTokens()
        {
            Assert.IsFalse(ApiTokenService.LooksValid(null));
            Assert.IsFalse(ApiTokenService.LooksValid("abc"));
            Assert.IsFalse(ApiTokenService.LooksValid("tc_" + new string('a', 39)));
            Assert.IsFalse(ApiTokenService.LooksValid("tc_" + new string('a', 39) + "!"));
            Assert.IsFalse(ApiTokenService.LooksValid("xx_" + new string('a', 40)));
        }
    }
}
