using Application.Utilities;
using Xunit;

namespace Application.Tests;

public class AgencyCommissionPricingTests
{
  [Theory]
  [InlineData(50_000, 10, 45_000)]
  [InlineData(50_000, 0, 50_000)]
  [InlineData(2_729_000, 10, 2_456_100)]
  [InlineData(2_592_550, 10, 2_333_295)]
  [InlineData(100, 100, 0)]
  [InlineData(0, 10, 0)]
  [InlineData(999, 10, 899)]
  public void NetPayableTomans_matches_ors_deduction(int gross, int percent, int expected)
  {
    Assert.Equal(expected, AgencyCommissionPricing.NetPayableTomans(gross, percent));
  }
}
