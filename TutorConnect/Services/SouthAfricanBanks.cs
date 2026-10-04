using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TutorConnect.Services
{
    /// <summary>
    /// South African banks with their universal branch codes, used by the bank-payment checkout and the tutor's
    /// banking form. Only these public details live here; account numbers are never stored in full.
    /// </summary>
    public static class SouthAfricanBanks
    {
        public class Bank
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string BranchCode { get; set; }
            public string Initials { get; set; }
            public string Tone { get; set; }   // tile colour from the app's own palette (not bank branding)
            /// <summary>Allowed account-number lengths (digits). Most banks have exactly one.</summary>
            public int[] Lengths { get; set; }
            public int MaxLength => Lengths.Max();

            /// <summary>"10 digits", "9 or 11 digits", "8 to 11 digits".</summary>
            public string LengthText
            {
                get
                {
                    var l = Lengths.OrderBy(x => x).ToArray();
                    if (l.Length == 1) return l[0] + " digits";
                    var consecutive = l.Last() - l.First() == l.Length - 1;
                    if (l.Length > 2 && consecutive) return l.First() + " to " + l.Last() + " digits";
                    return string.Join(", ", l.Take(l.Length - 1)) + " or " + l.Last() + " digits";
                }
            }
        }

        /// <summary>
        /// Account-number lengths. Capitec and Nedbank (10), FNB and Investec (11), Standard Bank (9 old / 11 new) and
        /// Absa (8 to 11) follow Netcash's published "Account number tips". African Bank, Bank Zero, Discovery Bank and
        /// TymeBank aren't listed there, so they accept 10 or 11 digits; tighten them here if your bank confirms one length.
        /// </summary>
        public static readonly List<Bank> All = new List<Bank>
        {
            new Bank { Lengths = new[] { 8, 9, 10, 11 }, Code = "absa", Name = "Absa", BranchCode = "632005", Initials = "AB", Tone = "#BE123C" },
            new Bank { Lengths = new[] { 10, 11 }, Code = "african", Name = "African Bank", BranchCode = "430000", Initials = "AF", Tone = "#15803D" },
            new Bank { Lengths = new[] { 10, 11 }, Code = "bankzero", Name = "Bank Zero", BranchCode = "888000", Initials = "BZ", Tone = "#334155" },
            new Bank { Lengths = new[] { 10 }, Code = "capitec", Name = "Capitec Bank", BranchCode = "470010", Initials = "CA", Tone = "#0369A1" },
            new Bank { Lengths = new[] { 10, 11 }, Code = "discovery", Name = "Discovery Bank", BranchCode = "679000", Initials = "DI", Tone = "#1D4ED8" },
            new Bank { Lengths = new[] { 11 }, Code = "fnb", Name = "First National Bank (FNB)", BranchCode = "250655", Initials = "FN", Tone = "#0E7490" },
            new Bank { Lengths = new[] { 11 }, Code = "investec", Name = "Investec", BranchCode = "580105", Initials = "IN", Tone = "#1B1840" },
            new Bank { Lengths = new[] { 10 }, Code = "nedbank", Name = "Nedbank", BranchCode = "198765", Initials = "NE", Tone = "#166534" },
            new Bank { Lengths = new[] { 9, 11 }, Code = "standard", Name = "Standard Bank", BranchCode = "051001", Initials = "SB", Tone = "#1E40AF" },
            new Bank { Lengths = new[] { 10, 11 }, Code = "tyme", Name = "TymeBank", BranchCode = "678910", Initials = "TY", Tone = "#B45309" }
        };

        public static readonly string[] AccountTypes = { "Cheque / current", "Savings", "Transmission" };

        public static Bank Find(string code)
        {
            return All.FirstOrDefault(b => b.Code == code);
        }

        /// <summary>Finds a bank by its name, including older short names such as "ABSA", "Capitec" or "FNB".</summary>
        public static Bank FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var n = name.Trim().ToLowerInvariant();
            var exact = All.FirstOrDefault(b => b.Name.ToLowerInvariant() == n);
            if (exact != null) return exact;
            if (n.Contains("fnb") || n.Contains("first national") || n.Contains("first rand")) return Find("fnb");
            if (n.Contains("tyme")) return Find("tyme");
            if (n.Contains("standard")) return Find("standard");
            return All.FirstOrDefault(b => b.Name.ToLowerInvariant().StartsWith(n) || n.StartsWith(b.Name.ToLowerInvariant().Split(' ')[0]));
        }

        /// <summary>True when the number has exactly one of the lengths this bank uses.</summary>
        public static bool IsValidAccountNumber(Bank bank, string accountNumber)
        {
            var d = Digits(accountNumber);
            if (!Regex.IsMatch(d, @"^\d+$")) return false;
            return bank == null ? d.Length >= 6 && d.Length <= 16 : bank.Lengths.Contains(d.Length);
        }

        /// <summary>A message saying exactly what's wrong with the number for this bank, or null when it's fine.</summary>
        public static string AccountNumberProblem(Bank bank, string accountNumber)
        {
            var d = Digits(accountNumber);
            if (d.Length == 0) return "Enter the account number.";
            if (!Regex.IsMatch(d, @"^\d+$")) return "Account numbers contain digits only.";
            if (IsValidAccountNumber(bank, accountNumber)) return null;
            return bank == null
                ? "Enter a valid account number (6 to 16 digits)."
                : bank.Name + " account numbers are " + bank.LengthText + ". You entered " + d.Length + ".";
        }

        /// <summary>Digits only (spaces and dashes removed).</summary>
        public static string Digits(string accountNumber)
        {
            return Regex.Replace(accountNumber ?? "", @"[\s-]", "");
        }

        /// <summary>South African bank account numbers are 6 to 16 digits.</summary>
        public static bool IsValidAccountNumber(string accountNumber)
        {
            return Regex.IsMatch(Digits(accountNumber), @"^\d{6,16}$");
        }

        public static string Last4(string accountNumber)
        {
            var d = Digits(accountNumber);
            return d.Length >= 4 ? d.Substring(d.Length - 4) : d;
        }
    }
}
