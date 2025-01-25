namespace Application.Models
{
  public class ChargePaymentRequest
  {
    public int Id { get; set; }
    public string Amout { get; set; }
    public string? Message { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime RequestedOn { set; get; }

    public Agency Agency { get; set; }
  }
}
