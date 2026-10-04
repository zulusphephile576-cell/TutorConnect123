using System;
using System.Data;
using TutorConnect.Data;

namespace TutorConnect.Services.Payouts
{
    /// <summary>
    /// Creates (or reuses) the payout row for a completed payment and sends it through the configured provider.
    /// Statuses: Queued -> Sent | Failed | NoBankDetails. Safe to call repeatedly.
    /// </summary>
    public static class PayoutService
    {
        public static string Process(int paymentId)
        {
            var table = Db.Query("sp_Payout_Create", Db.P("@PaymentId", paymentId));
            if (table.Rows.Count == 0) return null;
            DataRow r = table.Rows[0];

            var payoutId = Convert.ToInt32(r["PayoutId"]);
            var status = Convert.ToString(r["Status"]);
            if (status == "Sent") return status;

            var accountNumber = SecretProtector.Unprotect(r["AccountNumberProtected"] as string);
            if (r["BankName"] == DBNull.Value || string.IsNullOrEmpty(accountNumber))
            {
                Update(payoutId, "NoBankDetails", null, "Waiting for the tutor to add banking details. It will be sent automatically once they do.");
                return "NoBankDetails";
            }

            var provider = PayoutProviderFactory.Create();
            PayoutResult result;
            try
            {
                result = provider.Send(new PayoutRequest
                {
                    PayoutId = payoutId,
                    PaymentRef = Convert.ToString(r["PaymentRef"]),
                    Amount = Convert.ToDecimal(r["Amount"]),
                    TutorName = Convert.ToString(r["TutorName"]),
                    BankName = Convert.ToString(r["BankName"]),
                    AccountHolder = Convert.ToString(r["AccountHolder"]),
                    AccountType = Convert.ToString(r["AccountType"]),
                    BranchCode = Convert.ToString(r["BranchCode"]),
                    AccountNumber = accountNumber,
                    Reference = "TutorConnect " + Convert.ToString(r["PaymentRef"])
                });
            }
            catch (Exception ex)
            {
                AppLog.Error("Payout provider " + provider.Name, ex);
                result = new PayoutResult { Success = false, Message = "Provider error: " + ex.Message };
            }

            var newStatus = result.Success ? "Sent" : "Failed";
            Update(payoutId, newStatus, result.ProviderReference, result.Message);
            AppLog.Write("PAYOUT", "Payout " + payoutId + " for payment " + paymentId + ": " + newStatus + " via " + provider.Name);
            return newStatus;
        }

        /// <summary>Called after a tutor saves banking details: sends anything that was waiting.</summary>
        public static int ProcessPendingForTutor(int tutorId)
        {
            var sent = 0;
            foreach (DataRow row in Db.Query("sp_Payout_PendingForTutor", Db.P("@TutorId", tutorId)).Rows)
            {
                try
                {
                    if (Process(Convert.ToInt32(row["PaymentId"])) == "Sent") sent++;
                }
                catch (Exception ex)
                {
                    AppLog.Error("Pending payout for tutor " + tutorId, ex);
                }
            }
            return sent;
        }

        private static void Update(int payoutId, string status, string reference, string message)
        {
            Db.Exec("sp_Payout_Update", Db.P("@PayoutId", payoutId), Db.P("@Status", status),
                Db.P("@ProviderReference", reference), Db.P("@Message", message));
        }
    }
}
