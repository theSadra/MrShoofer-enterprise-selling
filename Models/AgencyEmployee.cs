using System.ComponentModel.DataAnnotations;

namespace Application.Models
{
  public class AgencyEmployee
  {
    public int Id { get; set; }

    public int AgencyId { get; set; }
    public Agency Agency { get; set; } = null!;

    [Required]
    public string Firstname { get; set; } = "";

    [Required]
    public string Lastname { get; set; } = "";

    [Required]
    public string Gender { get; set; } = "";

    [Required]
    public string NaCode { get; set; } = "";

    [Required]
    public string PhoneNumber { get; set; } = "";

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
  }
}
