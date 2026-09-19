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

    /// <summary>Seller = فروشنده عادی؛ Organization = پنل سازمانی با فاکتور رسمی.</summary>
    public AgencyPanelType PanelType { get; set; } = AgencyPanelType.Seller;

    public bool IsOrganization => PanelType == AgencyPanelType.Organization;

    public string? IdentityUserId { get; set; }

    /// <summary>کد اقتصادی سازمان (اختیاری — برای فاکتور رسمی)</summary>
    public string? EconomicNo { get; set; }
    /// <summary>شماره ثبت سازمان (اختیاری)</summary>
    public string? RegistrationNo { get; set; }
    /// <summary>شناسه ملی سازمان (اختیاری)</summary>
    public string? NationalId { get; set; }
    /// <summary>نمابر (اختیاری)</summary>
    public string? Fax { get; set; }
    /// <summary>استان (اختیاری)</summary>
    public string? Province { get; set; }
    /// <summary>شهرستان (اختیاری)</summary>
    public string? County { get; set; }
    /// <summary>شهر (اختیاری)</summary>
    public string? City { get; set; }
    /// <summary>کد پستی (اختیاری)</summary>
    public string? PostalCode { get; set; }

    /// <summary>True when org has filled at least one invoice buyer identifier.</summary>
    public bool HasInvoiceFinancialInfo =>
      !string.IsNullOrWhiteSpace(EconomicNo) ||
      !string.IsNullOrWhiteSpace(RegistrationNo) ||
      !string.IsNullOrWhiteSpace(NationalId) ||
      !string.IsNullOrWhiteSpace(Fax) ||
      !string.IsNullOrWhiteSpace(Province) ||
      !string.IsNullOrWhiteSpace(County) ||
      !string.IsNullOrWhiteSpace(City) ||
      !string.IsNullOrWhiteSpace(PostalCode);

    public IdentityUser IdentityUser { get; set; }
    public ICollection<Ticket> SoldTickets { get; set; }
    public ICollection<AgencyEmployee> Employees { get; set; }
  }
}
