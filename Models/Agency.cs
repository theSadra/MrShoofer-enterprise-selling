using Microsoft.AspNetCore.Identity;

namespace Application.Models
{
  public class Agency
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Address { get; set; }
    public string AdminMobile { get; set; }
    public DateTime DateJoined { get; set; }
    public string ORSAPI_token { set; get; }
    public int Commission { get; set; }

    public string? IdentityUserId { get; set; }

    /// <summary>کد اقتصادی سازمان فروشنده (آژانس)</summary>
    public string? EconomicNo { get; set; }
    /// <summary>شماره ثبت سازمان فروشنده (آژانس)</summary>
    public string? RegistrationNo { get; set; }
    /// <summary>شناسه ملی / کد ملی سازمان فروشنده (آژانس)</summary>
    public string? NationalId { get; set; }

    public IdentityUser IdentityUser { get; set; }
    public ICollection<Ticket> SoldTickets { get; set; }
    public ICollection<AgencyEmployee> Employees { get; set; }
  }
}
