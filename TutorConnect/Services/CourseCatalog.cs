using System.Collections.Generic;
using System.Linq;
using TutorConnect.Data;
using TutorConnect.Models;

namespace TutorConnect.Services
{
    /// <summary>
    /// Faculty -> department -> course. Every screen that asks for a course uses this, and the server re-checks the
    /// chain so a department from another faculty (or a course from another department) can never be submitted.
    /// </summary>
    public class CourseCatalog
    {
        public List<Faculty> Faculties { get; set; } = new List<Faculty>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Course> Courses { get; set; } = new List<Course>();

        public static CourseCatalog Load()
        {
            return new CourseCatalog
            {
                Faculties = Db.List<Faculty>("sp_Faculty_List"),
                Departments = Db.List<Department>("sp_Department_List", Db.P("@FacultyId", null)),
                Courses = Db.List<Course>("sp_Course_List")
            };
        }

        /// <summary>
        /// Checks that the faculty exists, the department belongs to that faculty and, when a course is required,
        /// that the course belongs to that department. Returns (field, message) pairs; empty means valid.
        /// </summary>
        public static List<KeyValuePair<string, string>> Validate(int? facultyId, int? departmentId, int? courseId,
            IEnumerable<Faculty> faculties, IEnumerable<Department> departments, IEnumerable<Course> courses, bool courseRequired = true)
        {
            var errors = new List<KeyValuePair<string, string>>();
            if (!facultyId.HasValue || faculties.All(f => f.FacultyId != facultyId.Value))
            {
                errors.Add(new KeyValuePair<string, string>("FacultyId", "Choose your faculty."));
                return errors;
            }
            var department = departmentId.HasValue ? departments.FirstOrDefault(d => d.DepartmentId == departmentId.Value) : null;
            if (department == null)
            {
                errors.Add(new KeyValuePair<string, string>("DepartmentId", "Choose a department in the selected faculty."));
                return errors;
            }
            if (department.FacultyId != facultyId.Value)
            {
                errors.Add(new KeyValuePair<string, string>("DepartmentId", "That department isn't part of the selected faculty."));
                return errors;
            }
            if (!courseRequired) return errors;

            var course = courseId.HasValue ? courses.FirstOrDefault(c => c.CourseId == courseId.Value) : null;
            if (course == null)
                errors.Add(new KeyValuePair<string, string>("CourseId", "Choose a course in the selected department."));
            else if (course.DepartmentId != department.DepartmentId)
                errors.Add(new KeyValuePair<string, string>("CourseId", "That course isn't offered by the selected department."));
            return errors;
        }

        public List<KeyValuePair<string, string>> Validate(int? facultyId, int? departmentId, int? courseId, bool courseRequired = true)
        {
            return Validate(facultyId, departmentId, courseId, Faculties, Departments, Courses, courseRequired);
        }

        /// <summary>For pre-filling: the faculty and department of an existing course.</summary>
        public void Locate(int? courseId, out int? facultyId, out int? departmentId)
        {
            var c = courseId.HasValue ? Courses.FirstOrDefault(x => x.CourseId == courseId.Value) : null;
            facultyId = c == null ? null : c.FacultyId;
            departmentId = c == null ? null : c.DepartmentId;
        }
    }
}
