using System.ComponentModel.DataAnnotations;

namespace Application.Models
{
  /// <summary>Extra passengers on a charter ticket (lead passenger stays on Ticket).</summary>
  public class TicketCompanion
  {
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int SortOrder { get; set; }

    [Required]
    public string Firstname { get; set; } = "";

    [Required]
    public string Lastname { get; set; } = "";

    [Required]
    public string Gender { get; set; } = "";

    [Required]
    public string NaCode { get; set; } = "";

    public string? PhoneNumber { get; set; }

    public int? AgencyEmployeeId { get; set; }
    public AgencyEmployee? AgencyEmployee { get; set; }
  }
}
