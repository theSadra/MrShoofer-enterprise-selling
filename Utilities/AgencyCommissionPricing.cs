namespace Application.Utilities
{
  /// <summary>
  /// Agency/OTA commission is a discount off the ORS list ticket price.
  /// Matches MrShoofer ORS ConfirmReserve:
  /// commissionAmount = FinalPrice * BaseCommission / 100m;
  /// amountToDeduct = FinalPrice - commissionAmount;
  /// </summary>
  public static class AgencyCommissionPricing
  {
    public static int NetPayableTomans(int grossTomans, int commissionPercent)
    {
      if (grossTomans <= 0) return 0;
      if (commissionPercent <= 0) return grossTomans;
      if (commissionPercent >= 100) return 0;

      // Keep decimal math like ORS, then round to nearest toman for UI/payment.
      var commissionAmount = (decimal)grossTomans * commissionPercent / 100m;
      var amountToDeduct = (decimal)grossTomans - commissionAmount;
      return (int)Math.Round(amountToDeduct, MidpointRounding.AwayFromZero);
    }

    public static int CommissionAmountTomans(int grossTomans, int commissionPercent)
    {
      if (grossTomans <= 0 || commissionPercent <= 0) return 0;
      if (commissionPercent >= 100) return grossTomans;
      var commissionAmount = (decimal)grossTomans * commissionPercent / 100m;
      return (int)Math.Round(commissionAmount, MidpointRounding.AwayFromZero);
    }
  }
}
