using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Agency
{
  public class AgencyProfileEditViewModel
  {
    [Required(ErrorMessage = "نام آژانس الزامی است")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "تلفن مدیر الزامی است")]
    [StringLength(20)]
    public string AdminMobile { get; set; } = "";

    [Required(ErrorMessage = "آدرس الزامی است")]
    [StringLength(500)]
    public string Address { get; set; } = "";

    public string? EconomicNo { get; set; }
    public string? RegistrationNo { get; set; }
    public string? NationalId { get; set; }
    public string? Fax { get; set; }
    public string? Province { get; set; }
    public string? County { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
  }
}
