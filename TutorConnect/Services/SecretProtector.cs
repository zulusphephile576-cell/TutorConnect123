using System;
using System.Text;
using System.Web.Security;

namespace TutorConnect.Services
{
    /// <summary>
    /// Encrypts sensitive values (bank account numbers) with the ASP.NET machine key.
    /// Set an explicit machineKey in Web.config so values survive app restarts and server moves.
    /// </summary>
    public static class SecretProtector
    {
        private const string Purpose = "TutorConnect.BankingDetails";

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            return Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(plain), Purpose));
        }

        public static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue)) return null;
            try
            {
                return Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(protectedValue), Purpose));
            }
            catch (Exception ex)
            {
                AppLog.Error("SecretProtector.Unprotect", ex);
                return null;
            }
        }
    }
}
