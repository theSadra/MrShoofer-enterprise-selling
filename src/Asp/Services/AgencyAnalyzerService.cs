using Application.Migrations;

namespace Application.Services
{
  public class AgencyAnalyzerService
  {
    private Agency agency;

    public AgencyAnalyzerService(Agency agency)
    {
      this.agency = agency;
    }



    public int GetTotalSold()
    {
      return agency.SoldTickets.Count();
    }


    public int GetTodaySold()
    {
      return agency.SoldTickets
        .Where(t => t.RegisteredAt >= DateTime.Today && t.RegisteredAt < DateTime.Today.AddDays(1))
        .Count();
    }


    public int GetThisMonthSold()
    {
      var now = DateTime.Now;
      return agency.SoldTickets
          .Where(t => t.RegisteredAt.ToPersianDate().Year == now.ToPersianDate().Year && t.RegisteredAt.ToPersianDate().Month == now.ToPersianDate().Month)
          .Count();
    }


    public int GetLast7DaysSold()
    {
      var sevenDaysAgo = DateTime.Today.AddDays(-7);
      return agency.SoldTickets
          .Where(t => t.RegisteredAt >= sevenDaysAgo && t.RegisteredAt < DateTime.Today.AddDays(1))
          .Count();
    }


    public long GetThisMonthSoldTotalPrice()
    {
      var now = DateTime.Now;
      return agency.SoldTickets
          .Where(t => t.RegisteredAt.Year == now.Year && t.RegisteredAt.Month == now.Month)
          .Sum(t => t.TicketFinalPrice);
    }


    public long GetThisMonthTotalProfit()
    {
      var now = DateTime.Now;
      return agency.SoldTickets
          .Where(t => t.RegisteredAt.Year == now.Year && t.RegisteredAt.Month == now.Month)
          .Sum(t => t.TicketFinalPrice) * agency.Commission / 100;
    }


    public Dictionary<int, double> GetLast7Days_SaleChartPercentage()
    {
      var last7Days = Enumerable.Range(0, 7)
             .Select(i => DateTime.Today.AddDays(-i))
             .ToList();

      var result = agency.SoldTickets
          .Where(t => !t.IsCancelled && t.RegisteredAt >= last7Days.Last())
          .GroupBy(t => t.RegisteredAt.Date)
          .Select(g => new { Date = g.Key, Count = g.Count() })
          .ToList();

      var ticketSales = last7Days
          .Select((date, index) => new { Index = 6 - index, Count = result.FirstOrDefault(r => r.Date == date)?.Count ?? 0 })
          .ToDictionary(x => x.Index, x => x.Count);

      // Calculate the highest sales count
      int maxSales = ticketSales.Values.Max();

      // Convert counts to percentages with the highest sales being 80%
      var salesWithPercentages = ticketSales.ToDictionary(
          x => x.Key,
          x => maxSales > 0 ? Math.Round((x.Value / (double)maxSales) * 80, 2) : 0
      );

      return salesWithPercentages;

    }
  }
}
