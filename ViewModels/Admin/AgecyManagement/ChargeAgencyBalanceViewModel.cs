using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace Application.ViewModels.Admin.AgecyManagement
{
  public class ChargeAgencyBalanceViewModel
  {
    [Required]
    public int AgencyId { get; set; }
    [Required(ErrorMessage = "میزان شارژ حتما باید وارد شود")]
    public string Amount { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    public string PayemntId { get; set; }

    public string? Description { get; set; }
  }
}
