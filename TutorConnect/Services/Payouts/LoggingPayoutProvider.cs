using System;
using System.Globalization;

namespace TutorConnect.Services.Payouts
{
    /// <summary>
    /// Placeholder provider: records the payout instruction in App_Data/payouts.log and reports success.
    /// No money moves. It exists so the full automatic flow can be demonstrated end to end.
    /// </summary>
    public class LoggingPayoutProvider : IPayoutProvider
    {
        public string Name => "Logging (simulated)";

        public PayoutResult Send(PayoutRequest request)
        {
            var reference = "SIM-" + TutorConnect.Helpers.Clock.Now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + request.PayoutId;
            var masked = string.IsNullOrEmpty(request.AccountNumber) || request.AccountNumber.Length < 4
                ? "****"
                : new string('*', request.AccountNumber.Length - 4) + request.AccountNumber.Substring(request.AccountNumber.Length - 4);

            AppLog.AppendTo("payouts.log", string.Format(CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss}\t{1}\tR{2:0.00}\t{3}\t{4} {5} ({6})\tbranch {7}\tref {8}",
                TutorConnect.Helpers.Clock.Now, reference, request.Amount, request.TutorName, request.BankName, masked, request.AccountType,
                request.BranchCode, request.Reference));

            return new PayoutResult
            {
                Success = true,
                ProviderReference = reference,
                Message = "Simulated transfer recorded. Connect a licensed payout provider to move real funds."
            };
        }
    }
}
