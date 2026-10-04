using System;
using System.Security.Cryptography;

namespace TutorConnect.Services
{
    /// <summary>BCrypt hashing (BCrypt.Net-Next). Plain-text passwords are never stored.</summary>
    public static class PasswordHasher
    {
        private const int WorkFactor = 11;

        public static string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Readable temporary password, e.g. "Tc-7hq4-Rm2x!".</summary>
        public static string GenerateTemporary()
        {
            const string chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var c = new char[8];
            for (int i = 0; i < 8; i++) c[i] = chars[bytes[i] % chars.Length];
            return "Tc-" + new string(c, 0, 4) + "-" + new string(c, 4, 4) + "!";
        }

        /// <summary>URL-safe random token (used for password reset links).</summary>
        public static string NewToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
