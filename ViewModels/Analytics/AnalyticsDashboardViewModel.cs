namespace Application.ViewModels.Analytics
{
  public class AnalyticsDailyPointViewModel
  {
    public string Label { get; set; } = "";
    public string IsoDate { get; set; } = "";
    public int Trips { get; set; }
    public int SpendToman { get; set; }
  }

  public class AnalyticsRouteRowViewModel
  {
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public int TripCount { get; set; }
    public int TotalToman { get; set; }
  }

  public class AnalyticsDashboardViewModel
  {
    public string FilterLabel { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string FromShamsi { get; set; } = "";
    public string ToShamsi { get; set; } = "";
    public List<int> PersianYears { get; set; } = new();

    public int GrandTrips { get; set; }
    public int GrandTotal { get; set; }
    public int PassengerCount { get; set; }
    public int AvgTicket { get; set; }
    public int CancelledCount { get; set; }
    public int ActiveTickets { get; set; }

    public List<AnalyticsDailyPointViewModel> DailySeries { get; set; } = new();
    public List<AnalyticsRouteRowViewModel> TopRoutes { get; set; } = new();
    public List<Application.ViewModels.Employees.EmployeeSpendRowViewModel> SpendRows { get; set; } = new();
  }
}
