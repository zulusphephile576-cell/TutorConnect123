using System;
using TutorConnect.Data;
using TutorConnect.Models;
using TutorConnect.Services.Payouts;

namespace TutorConnect.Services
{
    /// <summary>
    /// Runs once a payment is confirmed: creates the booking (via the idempotent stored procedure),
    /// notifies the tutor and triggers the automatic payout. No admin step is involved.
    /// </summary>
    public static class PaymentProcessor
    {
        public static int? Complete(string paymentRef, string pfPaymentId)
        {
            var row = Db.Row(Db.Query("sp_Payment_Complete", Db.P("@PaymentRef", paymentRef), Db.P("@PfPaymentId", pfPaymentId)));
            if (row.Count == 0) return null;

            var paymentId = Convert.ToInt32(row["PaymentId"]);
            var bookingId = row["BookingId"] == null ? (int?)null : Convert.ToInt32(row["BookingId"]);
            var wasNew = row["WasNew"] != null && Convert.ToBoolean(row["WasNew"]);

            if (wasNew)
            {
                AppLog.Write("PAYMENT", "Payment " + paymentRef + " complete (PayFast " + pfPaymentId + "), booking " + bookingId);
                NotifyTutor(paymentRef);
                try
                {
                    PayoutService.Process(paymentId);
                }
                catch (Exception ex)
                {
                    // The payment and booking are safe; the payout stays Queued and can be retried from Transactions.
                    AppLog.Error("Payout for payment " + paymentId, ex);
                }
            }
            return bookingId;
        }

        private static void NotifyTutor(string paymentRef)
        {
            try
            {
                var p = Db.Single<Payment>("sp_Payment_GetByRef", Db.P("@PaymentRef", paymentRef));
                if (p == null || !p.TutorWantsEmail) return;
                var body = "<p>" + EmailService.Encode(p.StudentName) + " has paid for a " + EmailService.Encode(p.ModuleCode) +
                           " session on <strong>" + p.SessionStart.ToString("dddd d MMMM, HH:mm") + "</strong> (" + p.Hours +
                           (p.Hours == 1 ? " hour" : " hours") + ").</p><p>Log in to TutorConnect to approve or decline the request.</p>" +
                           "<p><a href=\"" + AppConfig.BaseUrl + "/Tutor/Sessions\" style=\"color:#4F46E5\">Open my sessions</a></p>";
                EmailService.SendInBackground(p.TutorEmail, "New paid booking request", body);
            }
            catch (Exception ex)
            {
                AppLog.Error("NotifyTutor " + paymentRef, ex);
            }
        }
    }
}
