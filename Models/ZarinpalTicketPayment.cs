namespace Application.Models
{
  public class ZarinpalTicketPayment
  {
    public int Id { get; set; }
    public string Authority { get; set; } = "";
    /// <summary>Amount charged on Zarinpal (full ticket or hybrid remainder).</summary>
    public int AmountToman { get; set; }
    /// <summary>Wallet/ORS balance applied before gateway (hybrid). Zero for full zarinpal.</summary>
    public int WalletAppliedToman { get; set; }
    /// <summary>Full ticket price at payment start (wallet + gateway).</summary>
    public int TicketPriceToman { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public long? RefId { get; set; }
    public string? CardPan { get; set; }

    public string TripCode { get; set; } = "";
    public string Firstname { get; set; } = "";
    public string Lastname { get; set; } = "";
    public string Numberphone { get; set; } = "";
    public string Nacode { get; set; } = "";
    public string Gender { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public int? AgencyEmployeeId { get; set; }

    /// <summary>JSON array of companion passengers for hybrid/zarinpal pending payments.</summary>
    public string? CompanionsJson { get; set; }

    public int? TicketId { get; set; }
    public Agency Agency { get; set; } = null!;
  }
}
