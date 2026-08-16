namespace Application.Models
{
  public class ZarinpalTicketPayment
  {
    public int Id { get; set; }
    public string Authority { get; set; } = "";
    public int AmountToman { get; set; }
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

    public int? TicketId { get; set; }
    public Agency Agency { get; set; } = null!;
  }
}
