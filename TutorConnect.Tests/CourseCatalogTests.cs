using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Tests
{
    /// <summary>Faculty -> department -> course must be a consistent chain; the server rejects anything else.</summary>
    [TestClass]
    public class CourseCatalogTests
    {
        private static CourseCatalog Sample()
        {
            return new CourseCatalog
            {
                Faculties = new List<Faculty>
                {
                    new Faculty { FacultyId = 1, Name = "Faculty of Accounting and Informatics" },
                    new Faculty { FacultyId = 2, Name = "Faculty of Engineering and the Built Environment" }
                },
                Departments = new List<Department>
                {
                    new Department { DepartmentId = 10, FacultyId = 1, Name = "Information Technology" },
                    new Department { DepartmentId = 11, FacultyId = 1, Name = "Financial Accounting" },
                    new Department { DepartmentId = 20, FacultyId = 2, Name = "Civil Engineering" }
                },
                Courses = new List<Course>
                {
                    new Course { CourseId = 100, Code = "BSCIT", Name = "BSc in IT", DepartmentId = 10, FacultyId = 1 },
                    new Course { CourseId = 200, Code = "DCIV", Name = "Diploma in Civil Engineering", DepartmentId = 20, FacultyId = 2 }
                }
            };
        }

        private static string FirstError(IEnumerable<KeyValuePair<string, string>> errors)
        {
            var e = errors.FirstOrDefault();
            return e.Key;
        }

        [TestMethod]
        public void ConsistentChain_IsValid()
        {
            Assert.AreEqual(0, Sample().Validate(1, 10, 100).Count);
        }

        [TestMethod]
        public void MissingFaculty_IsRejectedFirst()
        {
            Assert.AreEqual("FacultyId", FirstError(Sample().Validate(null, 10, 100)));
        }

        [TestMethod]
        public void UnknownFaculty_IsRejected()
        {
            Assert.AreEqual("FacultyId", FirstError(Sample().Validate(99, 10, 100)));
        }

        [TestMethod]
        public void DepartmentRequiredOnceFacultyChosen()
        {
            Assert.AreEqual("DepartmentId", FirstError(Sample().Validate(1, null, 100)));
        }

        [TestMethod]
        public void DepartmentFromAnotherFaculty_IsRejected()
        {
            var errors = Sample().Validate(1, 20, 200);
            Assert.AreEqual("DepartmentId", FirstError(errors));
            StringAssert.Contains(errors[0].Value, "isn't part of the selected faculty");
        }

        [TestMethod]
        public void CourseFromAnotherDepartment_IsRejected()
        {
            var errors = Sample().Validate(1, 11, 100);   // BSc IT isn't offered by Financial Accounting
            Assert.AreEqual("CourseId", FirstError(errors));
        }

        [TestMethod]
        public void CourseRequiredWhenAsked()
        {
            Assert.AreEqual("CourseId", FirstError(Sample().Validate(1, 10, null)));
        }

        [TestMethod]
        public void CourseOptional_ForNewCourseForm()
        {
            // The admin's new-course form only needs a faculty and a department.
            Assert.AreEqual(0, Sample().Validate(2, 20, null, courseRequired: false).Count);
        }

        [TestMethod]
        public void Locate_FindsFacultyAndDepartmentOfACourse()
        {
            int? f, d;
            Sample().Locate(200, out f, out d);
            Assert.AreEqual(2, f);
            Assert.AreEqual(20, d);
            Sample().Locate(null, out f, out d);
            Assert.IsNull(f);
            Assert.IsNull(d);
        }
    }
}
