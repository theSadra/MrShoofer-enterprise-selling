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
          .Sum(t => t.TicketFinalPrice) * agency.Commission /100;
    }


  }
}
