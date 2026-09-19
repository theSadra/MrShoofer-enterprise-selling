using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Reserve
{
  public class CompanionPassengerViewModel
  {
    public string Firstname { get; set; } = "";
    public string Lastname { get; set; } = "";
    public string Gender { get; set; } = "";
    public string NaCode { get; set; } = "";

    [RegularExpression(@"^$|^((0?9)|(\+?989))\d{9}$", ErrorMessage = "شماره تلفن را صحیح وارد کنید")]
    public string? PhoneNumber { get; set; }

    public int? EmployeeId { get; set; }
  }
}
