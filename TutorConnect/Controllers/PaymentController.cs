using System;
using System.Linq;
using System.Web.Mvc;
using TutorConnect.Data;
using TutorConnect.Filters;
using TutorConnect.Models;
using TutorConnect.Services;

namespace TutorConnect.Controllers
{
    public class PaymentController : BaseController
    {
        /// <summary>
        /// Local development helper: PayFast can't reach localhost with its ITN, so when this is on
        /// (and only in sandbox mode) the return page completes the payment itself.
        /// </summary>
        private static bool SimulationEnabled => PayFastService.Sandbox && AppConfig.GetBool("PayFastSimulateItn");

        /// <summary>
        /// "BankTransfer" (default): TutorConnect's own bank-payment checkout. It is a simulation for development and
        /// demonstrations: it records the payment and runs the normal booking and payout steps, but no money moves.
        /// "PayFast": PayFast's hosted checkout (real payments when PayFast:Sandbox is false).
        /// </summary>
        private static bool UseBankCheckout => !string.Equals(AppConfig.Get("Payments:Mode", "BankTransfer"), "PayFast", StringComparison.OrdinalIgnoreCase);

        private BankCheckoutViewModel BankModel(Payment payment)
        {
            return new BankCheckoutViewModel
            {
                Payment = payment,
                TutorBanking = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", payment.TutorId)),
                Banks = SouthAfricanBanks.All,
                AccountType = SouthAfricanBanks.AccountTypes[0]
            };
        }

        /// <summary>Tells the tutor (notification + email) that a student couldn't pay them because their bank account is missing.</summary>
        private void AskTutorForBanking(Payment payment)
        {
            try
            {
                var row = Db.Row(Db.Query("sp_Tutor_BankingNeeded", Db.P("@TutorId", payment.TutorId), Db.P("@StudentName", payment.StudentName)));
                if (row.Count > 0 && row["EmailNotifications"] != null && Convert.ToBoolean(row["EmailNotifications"]))
                    EmailService.SendInBackground(Convert.ToString(row["Email"]), "Add your banking details to get booked",
                        "<p>Hi " + EmailService.Encode(Convert.ToString(row["FirstName"])) + ",</p><p>" + EmailService.Encode(payment.StudentName) +
                        " wants to book a " + EmailService.Encode(payment.ModuleCode) + " session with you, but payments can't be made to you until you add your bank account.</p>" +
                        "<p><a href=\"" + AppConfig.BaseUrl + "/Tutor/Banking\" style=\"color:#4F46E5\">Add my banking details</a></p>");
            }
            catch (Exception ex) { AppLog.Error("AskTutorForBanking", ex); }
        }

        private Payment OwnPayment(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            var p = Db.Single<Payment>("sp_Payment_GetByRef", Db.P("@PaymentRef", reference));
            return p != null && p.StudentId == CurrentUserId ? p : null;
        }

        [RoleAuthorize("Student")]
        public ActionResult Checkout(string reference, string method)
        {
            var payment = OwnPayment(reference);
            if (payment == null) return HttpNotFound();
            if (payment.Status == "Complete") return RedirectToAction("Return", new { reference });

            if (UseBankCheckout)
            {
                var vm = BankModel(payment);
                if (vm.TutorBanking == null) { AskTutorForBanking(payment); return View("TutorNotPayable", payment); }
                if (method == "eft") return View("BankCheckout", vm);
                return View("CardCheckout", new CardCheckoutViewModel { Payment = payment, TutorBanking = vm.TutorBanking });
            }

            return View(new CheckoutViewModel
            {
                Payment = payment,
                ProcessUrl = PayFastService.ProcessUrl,
                Fields = PayFastService.BuildCheckoutFields(payment),
                SimulationEnabled = SimulationEnabled
            });
        }

        /// <summary>
        /// Completes a card payment from the checkout. The full card number and CVV are never sent here (those fields
        /// have no name in the form); only the brand, last four digits and expiry arrive, and only brand and last four are kept.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Student")]
        public ActionResult PayByCard(string reference, string cardholderName, string cardBrand, string cardLast4, string cardExpiry,
            string street, string city, string postalCode, string country)
        {
            var payment = OwnPayment(reference);
            if (payment == null || !UseBankCheckout) return HttpNotFound();
            if (payment.Status == "Complete") return RedirectToAction("Return", new { reference });
            if (payment.Status != "Pending") { FlashError("This payment can no longer be made. Book the session again."); return RedirectToAction("Bookings", "Student"); }

            var vm = new CardCheckoutViewModel
            {
                Payment = payment, TutorBanking = Db.Single<BankingDetails>("sp_Banking_Get", Db.P("@TutorId", payment.TutorId)),
                CardholderName = cardholderName, Street = street, City = city, PostalCode = postalCode, Country = country ?? "South Africa"
            };
            if (vm.TutorBanking == null) { AskTutorForBanking(payment); return View("TutorNotPayable", payment); }

            vm.Error = CardProblem(cardholderName, cardBrand, cardLast4, cardExpiry, street, city, postalCode, country);
            if (vm.Error != null) return View("CardCheckout", vm);

            Db.ScalarInt("sp_Payment_SetPayer", Db.P("@PaymentRef", reference), Db.P("@StudentId", CurrentUserId),
                Db.P("@Method", "Card"), Db.P("@Bank", cardBrand), Db.P("@Last4", cardLast4));
            var transactionId = "CARD" + TutorConnect.Helpers.Clock.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999);
            if (!TryDb(() => PaymentProcessor.Complete(reference, transactionId)))
                return View("CardCheckout", vm);

            LogActivity(CurrentUserId, "You paid " + Helpers.Ui.Money(payment.Amount) + " to " + payment.TutorName + " with " + cardBrand + " •••• " + cardLast4 + " (ref " + reference + ").");
            return RedirectToAction("Return", new { reference });
        }

        /// <summary>Checks what the card form sends. Null when everything is acceptable.</summary>
        public static string CardProblem(string holder, string brand, string last4, string expiry, string street, string city, string postalCode, string country)
        {
            if (string.IsNullOrWhiteSpace(holder) || holder.Trim().Length < 3) return "Enter the cardholder name as it appears on the card.";
            if (!CardCheckoutViewModel.Brands.Contains(brand) || !System.Text.RegularExpressions.Regex.IsMatch(last4 ?? "", @"^\d{4}$"))
                return "Enter a valid Visa, Mastercard or American Express card number.";
            var m = System.Text.RegularExpressions.Regex.Match(expiry ?? "", @"^(\d{2})\s*/\s*(\d{2})$");
            if (!m.Success) return "Enter the expiry date as MM / YY.";
            var month = int.Parse(m.Groups[1].Value); var year = 2000 + int.Parse(m.Groups[2].Value);
            if (month < 1 || month > 12) return "The expiry month must be between 01 and 12.";
            var now = TutorConnect.Helpers.Clock.Now;
            if (year < now.Year || (year == now.Year && month < now.Month)) return "This card has expired.";
            if (year > now.Year + 20) return "Check the expiry year.";
            if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city)) return "Enter your billing street address and city.";
            if (string.IsNullOrWhiteSpace(country) || !CardCheckoutViewModel.Countries.Contains(country)) return "Choose your country.";
            if (country == "South Africa" && !System.Text.RegularExpressions.Regex.IsMatch((postalCode ?? "").Trim(), @"^\d{4}$"))
                return "South African postal codes have 4 digits.";
            if (string.IsNullOrWhiteSpace(postalCode)) return "Enter your postal code.";
            return null;
        }

        /// <summary>Completes a bank payment from the checkout. Only the bank name and last four digits are kept.</summary>
        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Student")]
        public ActionResult PayByBank(string reference, string bankCode, string accountHolder, string accountType, string accountNumber)
        {
            var payment = OwnPayment(reference);
            if (payment == null || !UseBankCheckout) return HttpNotFound();
            if (payment.Status == "Complete") return RedirectToAction("Return", new { reference });
            if (payment.Status != "Pending") { FlashError("This payment can no longer be made. Book the session again."); return RedirectToAction("Bookings", "Student"); }

            var vm = BankModel(payment);
            vm.BankCode = bankCode; vm.AccountHolder = accountHolder; vm.AccountType = accountType; vm.AccountNumber = accountNumber;
            if (vm.TutorBanking == null) { AskTutorForBanking(payment); return View("TutorNotPayable", payment); }

            var bank = SouthAfricanBanks.Find(bankCode);
            if (bank == null) vm.Error = "Choose your bank.";
            else if (string.IsNullOrWhiteSpace(accountHolder) || accountHolder.Trim().Length < 3) vm.Error = "Enter the account holder's name as it appears at the bank.";
            else if (!SouthAfricanBanks.AccountTypes.Contains(accountType)) vm.Error = "Choose the account type.";
            else if (SouthAfricanBanks.AccountNumberProblem(bank, accountNumber) != null) vm.Error = SouthAfricanBanks.AccountNumberProblem(bank, accountNumber);
            if (vm.Error != null) return View("BankCheckout", vm);

            var last4 = SouthAfricanBanks.Last4(accountNumber);
            Db.ScalarInt("sp_Payment_SetPayer", Db.P("@PaymentRef", reference), Db.P("@StudentId", CurrentUserId),
                Db.P("@Method", "EFT"), Db.P("@Bank", bank.Name), Db.P("@Last4", last4));
            var transactionId = "EFT" + TutorConnect.Helpers.Clock.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999);
            if (!TryDb(() => PaymentProcessor.Complete(reference, transactionId)))
                return View("BankCheckout", vm);

            LogActivity(CurrentUserId, "You paid " + Helpers.Ui.Money(payment.Amount) + " to " + payment.TutorName + " from " + bank.Name + " •••• " + last4 + " (ref " + reference + ").");
            return RedirectToAction("Return", new { reference });
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Student")]
        public ActionResult Simulate(string reference)
        {
            var payment = OwnPayment(reference);
            if (payment == null || !SimulationEnabled) return HttpNotFound();
            TryDb(() => PaymentProcessor.Complete(reference, "SIMULATED-" + TutorConnect.Helpers.Clock.Now.ToString("HHmmss")));
            return RedirectToAction("Return", new { reference });
        }

        [RoleAuthorize("Student")]
        public ActionResult Return(string reference)
        {
            var payment = OwnPayment(reference);
            if (payment == null) return HttpNotFound();

            if (payment.Status == "Pending" && SimulationEnabled)
            {
                TryDb(() => PaymentProcessor.Complete(reference, "SANDBOX-RETURN"));
                payment = OwnPayment(reference);
            }
            return View(payment);
        }

        [RoleAuthorize("Student")]
        public ActionResult Cancel(string reference)
        {
            var payment = OwnPayment(reference);
            if (payment == null) return HttpNotFound();
            Db.ScalarInt("sp_Payment_SetStatus", Db.P("@PaymentRef", reference), Db.P("@Status", "Cancelled"));
            Flash("Payment cancelled. Your slot wasn't booked, so you can choose another time.", "info");
            return RedirectToAction("Book", "Student", new { tutorId = payment.TutorId, moduleId = payment.ModuleId });
        }

        /// <summary>Lets the return page poll while it waits for the ITN.</summary>
        [RoleAuthorize("Student")]
        public ActionResult Status(string reference)
        {
            var payment = OwnPayment(reference);
            if (payment == null) return HttpNotFound();
            return Json(new { status = payment.Status }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// PayFast Instant Transaction Notification. Called server-to-server by PayFast, so no session
        /// or anti-forgery token: trust comes from signature + amount + server validation instead.
        /// </summary>
        [HttpPost, AllowAnonymous]
        public ActionResult Notify()
        {
            var form = Request.Form;
            var reference = form["m_payment_id"];
            try
            {
                var payment = string.IsNullOrEmpty(reference) ? null : Db.Single<Payment>("sp_Payment_GetByRef", Db.P("@PaymentRef", reference));
                if (payment == null)
                {
                    AppLog.Write("ITN", "Unknown payment reference '" + reference + "'");
                    return new HttpStatusCodeResult(200);
                }

                string reason;
                if (!PayFastService.VerifyNotification(form, payment.Amount, out reason))
                {
                    AppLog.Write("ITN", "Rejected ITN for " + reference + ": " + reason);
                    return new HttpStatusCodeResult(200);
                }

                var status = (form["payment_status"] ?? "").ToUpperInvariant();
                if (status == "COMPLETE")
                    PaymentProcessor.Complete(reference, form["pf_payment_id"]);
                else if (status == "CANCELLED")
                    Db.ScalarInt("sp_Payment_SetStatus", Db.P("@PaymentRef", reference), Db.P("@Status", "Cancelled"));
                else if (status == "FAILED")
                    Db.ScalarInt("sp_Payment_SetStatus", Db.P("@PaymentRef", reference), Db.P("@Status", "Failed"));

                AppLog.Write("ITN", "Accepted ITN for " + reference + " with status " + status);
            }
            catch (Exception ex)
            {
                AppLog.Error("ITN " + reference, ex);
            }
            // PayFast only needs a 200 response.
            return new HttpStatusCodeResult(200);
        }
    }
}
