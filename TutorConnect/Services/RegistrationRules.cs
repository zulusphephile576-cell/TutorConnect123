using System;
using System.Collections.Generic;
using System.Linq;
using TutorConnect.Models;

namespace TutorConnect.Services
{
    /// <summary>
    /// Business rules for student registration beyond simple field attributes.
    /// Kept free of MVC so they can be unit tested (see TutorConnect.Tests).
    /// </summary>
    public static class RegistrationRules
    {
        public const int MinimumAge = 14;
        public const int MaximumAge = 100;

        public static int AgeOn(DateTime dateOfBirth, DateTime today)
        {
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.Date.AddYears(-age)) age--;
            return age;
        }

        /// <summary>Returns (field, message) pairs; an empty list means the rules pass.</summary>
        public static List<KeyValuePair<string, string>> Validate(RegisterViewModel m, DateTime today)
        {
            var errors = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (f, msg) => errors.Add(new KeyValuePair<string, string>(f, msg));

            if (m.DateOfBirth.HasValue)
            {
                var dob = m.DateOfBirth.Value.Date;
                var age = AgeOn(dob, today);
                if (dob > today.Date || age < MinimumAge || age > MaximumAge)
                    add("DateOfBirth", "Enter a real date of birth. Students must be at least " + MinimumAge + ".");
            }
            if (!string.IsNullOrWhiteSpace(m.Gender) && !RegisterViewModel.Genders.Contains(m.Gender))
                add("Gender", "Choose an option from the list.");
            if (string.IsNullOrWhiteSpace(m.EmergencyContactName) != string.IsNullOrWhiteSpace(m.EmergencyContactPhone))
                add("EmergencyContactPhone", "Give both a name and a number for your emergency contact, or leave both empty.");
            if (!string.IsNullOrWhiteSpace(m.Phone) && !IsSouthAfricanPhone(m.Phone))
                add("Phone", "Enter a South African number, for example 071 234 5678 or +27 71 234 5678.");
            if (!string.IsNullOrWhiteSpace(m.EmergencyContactPhone) && !IsSouthAfricanPhone(m.EmergencyContactPhone))
                add("EmergencyContactPhone", "Enter a South African number, for example 071 234 5678.");
            if (!m.AcceptTerms)
                add("AcceptTerms", "Tick the box to confirm your details are correct.");
            if (!m.AcceptPrivacy)
                add("AcceptPrivacy", "You need to accept the privacy notice to create an account.");
            return errors;
        }

        /// <summary>South African numbers: 0XX XXX XXXX or +27 XX XXX XXXX (spaces, dashes and brackets ignored).</summary>
        public static bool IsSouthAfricanPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (digits.StartsWith("+27")) digits = "0" + digits.Substring(3);
            else if (digits.StartsWith("27") && digits.Length == 11) digits = "0" + digits.Substring(2);
            return digits.Length == 10 && digits[0] == '0' && digits[1] != '0' && digits.All(char.IsDigit);
        }
    }
}
