using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>Clicking a notification opens the page it's about, for the signed-in role.</summary>
    [TestClass]
    public class NotificationLinksTests
    {
        [TestMethod]
        public void StoredLinkWins()
        {
            Assert.AreEqual("/Student/Content", NotificationLinks.For("Student", "anything", "/Student/Content"));
        }

        [TestMethod]
        public void StoredStudentLink_IsMappedForOtherRoles()
        {
            Assert.AreEqual("/Tutor/Content", NotificationLinks.For("Tutor", "New course material", "/Student/Content"));
        }

        [TestMethod]
        public void UnsafeStoredLink_IsIgnored()
        {
            Assert.AreEqual("/Student/Bookings", NotificationLinks.For("Student", "Your ITPR101 session on Tue was confirmed", "https://evil.example"));
        }

        [TestMethod]
        public void Bookings_GoToEachRolesPage()
        {
            Assert.AreEqual("/Tutor/Sessions", NotificationLinks.For("Tutor", "New paid booking request for ITPR101 on Tue 29 Sep", null));
            Assert.AreEqual("/Student/Bookings", NotificationLinks.For("Student", "Payment sent. Your session request was sent to your tutor.", null));
        }

        [TestMethod]
        public void Tutorials_GoToTutorials()
        {
            Assert.AreEqual("/Student/Tutorials", NotificationLinks.For("Student", "Tutorial updated: Loops (ITPR101) is now on Thu", null));
            Assert.AreEqual("/Student/Tutorials", NotificationLinks.For("Student", "You checked in to \"Loops\"", null));
        }

        [TestMethod]
        public void Applications_GoToTheRightList()
        {
            Assert.AreEqual("/Lecturer/Applications", NotificationLinks.For("Lecturer", "Please review the tutor application from L Zulu", null));
            Assert.AreEqual("/TutorApplication/Status", NotificationLinks.For("Student", "Your tutor application was submitted.", null));
        }

        [TestMethod]
        public void BankingAndAccount()
        {
            Assert.AreEqual("/Tutor/Banking", NotificationLinks.For("Tutor", "Add your banking details: L Zulu tried to book", null));
            Assert.AreEqual("/Account/Settings", NotificationLinks.For("Student", "You changed your password.", null));
        }

        [TestMethod]
        public void Unknown_OpensTheNotificationsPage()
        {
            Assert.AreEqual(NotificationLinks.Fallback, NotificationLinks.For("Admin", "Something unusual happened", null));
        }
    }
}
