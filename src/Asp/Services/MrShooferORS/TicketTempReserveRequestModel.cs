using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;

namespace Application.Services.MrShooferORS
{
  public class TicketTempReserveRequestModel
  {
    public string tripCode { get; set; }
    public bool isPrivate { get; set; }
    public int? seatnumber { get; set; }
  }
}
