
namespace Application.Models
{
  public class AgencyBalanceCharge
  {
    public int Id { get; set; }
    public int Amount { get; set; }
    public DateTime ChargedAt { get; set; }
    public string? PaymentID { get; set; }
    public string? Description { get; set; }

    // Nav prop
    public Agency Agency { get; set; }
  }
}
