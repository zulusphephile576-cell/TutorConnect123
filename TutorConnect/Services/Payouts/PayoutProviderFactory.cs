namespace TutorConnect.Services.Payouts
{
    public static class PayoutProviderFactory
    {
        /// <summary>Reads the "PayoutProvider" appSetting. Add a case here when a real provider exists.</summary>
        public static IPayoutProvider Create()
        {
            switch ((AppConfig.Get("PayoutProvider", "Logging") ?? "").ToLowerInvariant())
            {
                // case "stitch": return new StitchPayoutProvider(AppConfig.Get("Stitch:ClientId"), AppConfig.Get("Stitch:ClientSecret"));
                default:
                    return new LoggingPayoutProvider();
            }
        }
    }
}
