using System.IO;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>Upload checks run before anything is written to disk.</summary>
    [TestClass]
    public class FileStorageTests
    {
        private class FakeFile : HttpPostedFileBase
        {
            private readonly string _name; private readonly int _length;
            public FakeFile(string name, int length) { _name = name; _length = length; }
            public override string FileName { get { return _name; } }
            public override int ContentLength { get { return _length; } }
            public override Stream InputStream { get { return new MemoryStream(new byte[0]); } }
        }

        [TestMethod]
        public void Save_RejectsMissingFile()
        {
            string error;
            Assert.IsNull(FileStorage.Save(null, "applications", FileStorage.DocumentExtensions, 10, out error));
            Assert.AreEqual("Choose a file to upload.", error);
        }

        [TestMethod]
        public void Save_RejectsDisallowedType()
        {
            string error;
            Assert.IsNull(FileStorage.Save(new FakeFile("virus.exe", 100), "applications", FileStorage.DocumentExtensions, 10, out error));
            StringAssert.Contains(error, "isn't allowed");
        }

        [TestMethod]
        public void Save_RejectsOversizedFile()
        {
            string error;
            Assert.IsNull(FileStorage.Save(new FakeFile("id.pdf", 11 * 1024 * 1024), "applications", FileStorage.DocumentExtensions, 10, out error));
            StringAssert.Contains(error, "10 MB");
        }

        [TestMethod]
        public void DocumentTypes_IncludePhonePhotoFormats()
        {
            CollectionAssert.Contains(FileStorage.DocumentExtensions, ".heic");
            CollectionAssert.Contains(FileStorage.DocumentExtensions, ".pdf");
            CollectionAssert.DoesNotContain(FileStorage.DocumentExtensions, ".exe");
        }
    }
}
