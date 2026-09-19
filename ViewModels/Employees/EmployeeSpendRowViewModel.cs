namespace Application.ViewModels.Employees
{
  public class EmployeeSpendRowViewModel
  {
    public int? AgencyEmployeeId { get; set; }
    public string DisplayName { get; set; } = "";
    public int TripCount { get; set; }
    public int TotalToman { get; set; }
  }
}
