using System.Text.Json.Serialization;

namespace Application.Services.MrShooferORS
{
  public class OrsAgencyInfo
  {
    public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? NumberPhone { get; set; }
    public string? CompanyAddress { get; set; }

    [JsonPropertyName("baseCommission")]
    public decimal BaseCommission { get; set; }

    public long? AccountBalance { get; set; }
  }
}
