namespace Application.Models
{
  public class ZarinpalChargeRequest
  {
    public int Id { get; set; }
    public string Authority { get; set; }
    public int AmountToman { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Success | Failed
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public long? RefId { get; set; }
    public string? CardPan { get; set; }

    public Agency Agency { get; set; }
  }
}
