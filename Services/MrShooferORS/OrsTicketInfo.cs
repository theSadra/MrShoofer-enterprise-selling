using System.Text.Json.Serialization;

namespace Application.Services.MrShooferORS
{
  /// <summary>Subset of ORS /Tickets/getTicketInfo used for local status sync.</summary>
  public class OrsTicketInfo
  {
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("ticketCode")]
    public string? TicketCode { get; set; }

    [JsonPropertyName("tripPlanCode")]
    public string? TripPlanCode { get; set; }

    [JsonPropertyName("tripInfo")]
    public OrsTicketTripInfo? TripInfo { get; set; }

    public bool IsCancelled =>
      string.Equals(Status, "canceled", StringComparison.OrdinalIgnoreCase)
      || string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);

    public bool IsDone =>
      string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase)
      || string.Equals(Status, "done", StringComparison.OrdinalIgnoreCase);
  }

  public class OrsTicketTripInfo
  {
    [JsonPropertyName("startingDateTime")]
    public DateTime StartingDateTime { get; set; }
  }
}
