using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TutorConnect.Data;
using TutorConnect.Helpers;
using TutorConnect.Models;

namespace TutorConnect.Services
{
    /// <summary>
    /// Personal access tokens for the REST API. The plain token is shown to the user once;
    /// only its SHA-256 hash is stored, so a database leak doesn't expose usable tokens.
    /// </summary>
    public static class ApiTokenService
    {
        public const string Prefix = "tc_";
        public const int DefaultDays = 30;

        public static string Generate()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[40];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Prefix + new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        public static string Hash(string token)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? "")).Select(b => b.ToString("x2")));
        }

        public static bool LooksValid(string token)
        {
            return !string.IsNullOrEmpty(token) && token.StartsWith(Prefix) && token.Length == Prefix.Length + 40
                   && token.Skip(Prefix.Length).All(char.IsLetterOrDigit);
        }

        /// <summary>Creates a token and returns the plain value (the only time it is ever available).</summary>
        public static string Create(int userId, string name, int days, out DateTime expiresAt)
        {
            var token = Generate();
            expiresAt = Clock.Now.AddDays(Math.Max(1, Math.Min(365, days)));
            Db.ScalarInt("sp_ApiToken_Create", Db.P("@UserId", userId), Db.P("@TokenHash", Hash(token)),
                Db.P("@Name", string.IsNullOrWhiteSpace(name) ? "API token" : name.Trim()), Db.P("@ExpiresAt", expiresAt));
            return token;
        }

        public static ApiUser Validate(string token)
        {
            if (!LooksValid(token)) return null;
            return Db.Single<ApiUser>("sp_ApiToken_Validate", Db.P("@TokenHash", Hash(token)));
        }
    }
}
