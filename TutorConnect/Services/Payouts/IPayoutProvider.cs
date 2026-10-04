namespace TutorConnect.Services.Payouts
{
    /// <summary>
    /// The single seam between TutorConnect and whoever actually moves money.
    /// Swap LoggingPayoutProvider for a licensed provider (e.g. Stitch PayOuts) by implementing
    /// this interface and changing the "PayoutProvider" appSetting. Nothing else changes.
    /// </summary>
    public interface IPayoutProvider
    {
        string Name { get; }
        PayoutResult Send(PayoutRequest request);
    }

    public class PayoutRequest
    {
        public int PayoutId { get; set; }
        public string PaymentRef { get; set; }
        public decimal Amount { get; set; }
        public string TutorName { get; set; }
        public string BankName { get; set; }
        public string AccountHolder { get; set; }
        public string AccountType { get; set; }
        public string BranchCode { get; set; }
        public string AccountNumber { get; set; }
        public string Reference { get; set; }
    }

    public class PayoutResult
    {
        public bool Success { get; set; }
        public string ProviderReference { get; set; }
        public string Message { get; set; }
    }
}
