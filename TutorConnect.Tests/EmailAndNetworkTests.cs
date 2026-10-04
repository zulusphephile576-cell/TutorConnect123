using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>
    /// App.config deliberately contains the mistakes seen in practice (an email address as Smtp:Host, port 465,
    /// sending switched off) so these tests prove the app corrects or reports them.
    /// </summary>
    [TestClass]
    public class EmailAndNetworkTests
    {
        [TestMethod]
        public void Host_EmailAddressIsCorrectedToGmailServer()
        {
            Assert.AreEqual("smtp.gmail.com", EmailService.Host);
        }

        [TestMethod]
        public void Port_465IsReplacedWith587()
        {
            Assert.AreEqual(587, EmailService.Port);
        }

        [TestMethod]
        public void ConfigProblems_ExplainEachMistake()
        {
            var problems = EmailService.ConfigProblems();
            Assert.IsTrue(problems.Any(p => p.Contains("Smtp:Enabled")), "reports sending switched off");
            Assert.IsTrue(problems.Any(p => p.Contains("Smtp:Host contains an email address")), "reports the host mistake");
            Assert.IsTrue(problems.Any(p => p.Contains("465")), "reports the port");
        }

        [TestMethod]
        public void From_FallsBackToUserWhenNotAnAddress()
        {
            Assert.AreEqual(EmailService.User, EmailService.From);
        }

        [TestMethod]
        public void IsLocalHost_RecognisesLoopbackNames()
        {
            Assert.IsTrue(NetworkAddress.IsLocalHost("localhost"));
            Assert.IsTrue(NetworkAddress.IsLocalHost("LOCALHOST"));
            Assert.IsTrue(NetworkAddress.IsLocalHost("127.0.0.1"));
            Assert.IsFalse(NetworkAddress.IsLocalHost("192.168.1.20"));
        }

        [TestMethod]
        public void PhoneBaseUrl_KeepsRealAddresses()
        {
            Assert.AreEqual("http://192.168.1.20:44380", NetworkAddress.PhoneBaseUrl(new Uri("http://192.168.1.20:44380/Tutor/QrCode/5"), "/"));
        }

        [TestMethod]
        public void PhoneBaseUrl_ReplacesLocalhostWithNetworkAddress()
        {
            var url = NetworkAddress.PhoneBaseUrl(new Uri("http://localhost:44380/Tutor/QrCode/5"), "/");
            if (NetworkAddress.LanIPv4() != null)
                StringAssert.StartsWith(url, "http://" + NetworkAddress.LanIPv4() + ":44380");
            else
                Assert.AreEqual("http://localhost:44380", url);   // no network: nothing better to offer
        }

        [TestMethod]
        public void OllamaCandidates_TryIPv4BeforeLocalhost()
        {
            var list = AiService.OllamaCandidates();
            StringAssert.StartsWith(list[0], "http://127.0.0.1");
        }
    }
}
