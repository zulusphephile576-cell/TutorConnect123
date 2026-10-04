using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>PayFast signs requests with MD5 over PHP-style URL encoding; these values were computed independently.</summary>
    [TestClass]
    public class PayFastServiceTests
    {
        private static List<KeyValuePair<string, string>> SampleFields()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("merchant_id", "10000100"),
                new KeyValuePair<string, string>("merchant_key", "46f0cd694581a"),
                new KeyValuePair<string, string>("amount", "210.00"),
                new KeyValuePair<string, string>("item_name", "Tutoring: ITPR101 (2 hours)")
            };
        }

        [TestMethod]
        public void PhpUrlEncode_EncodesLikePhp()
        {
            Assert.AreEqual("Tom+%26+Jerry%27s+%28test%29%21%2A%7E", PayFastService.PhpUrlEncode("Tom & Jerry's (test)!*~"));
        }

        [TestMethod]
        public void PhpUrlEncode_KeepsUnreservedCharacters()
        {
            Assert.AreEqual("abc-XYZ_09.", PayFastService.PhpUrlEncode("abc-XYZ_09."));
        }

        [TestMethod]
        public void PhpUrlEncode_UsesUpperCaseHex()
        {
            Assert.AreEqual("%3A%2F", PayFastService.PhpUrlEncode(":/"));
        }

        [TestMethod]
        public void Signature_WithoutPassphrase_MatchesKnownValue()
        {
            Assert.AreEqual("3e5174256ea2417113724159e35b740e", PayFastService.Signature(SampleFields(), null));
        }

        [TestMethod]
        public void Signature_WithPassphrase_MatchesKnownValue()
        {
            Assert.AreEqual("64400d41b494e5311909ba960598345d", PayFastService.Signature(SampleFields(), "jt7NOE43FZPn"));
        }

        [TestMethod]
        public void Signature_ChangesWhenAmountIsTampered()
        {
            var fields = SampleFields();
            var original = PayFastService.Signature(fields, null);
            fields[2] = new KeyValuePair<string, string>("amount", "1.00");
            Assert.AreNotEqual(original, PayFastService.Signature(fields, null));
        }

        [TestMethod]
        public void VerifyNotification_RejectsForgedSignature()
        {
            var form = new NameValueCollection { { "m_payment_id", "TC123" }, { "amount_gross", "210.00" }, { "payment_status", "COMPLETE" }, { "signature", "forged" } };
            string reason;
            Assert.IsFalse(PayFastService.VerifyNotification(form, 210m, out reason));
            Assert.AreEqual("Signature mismatch", reason);
        }

        [TestMethod]
        public void VerifyNotification_RejectsWrongAmountEvenWithValidSignature()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("m_payment_id", "TC123"),
                new KeyValuePair<string, string>("amount_gross", "1.00"),
                new KeyValuePair<string, string>("payment_status", "COMPLETE")
            };
            var form = new NameValueCollection();
            foreach (var f in fields) form.Add(f.Key, f.Value);
            form.Add("signature", PayFastService.Signature(fields, null));

            string reason;
            Assert.IsFalse(PayFastService.VerifyNotification(form, 210m, out reason));
            Assert.AreEqual("Amount mismatch", reason);
        }

        [TestMethod]
        public void NewReference_IsUniqueAndPrefixed()
        {
            var a = PayFastService.NewReference();
            var b = PayFastService.NewReference();
            StringAssert.StartsWith(a, "TC");
            Assert.AreNotEqual(a, b);
            Assert.IsTrue(a.Length <= 40, "Must fit the PaymentRef column");
        }
    }
}
