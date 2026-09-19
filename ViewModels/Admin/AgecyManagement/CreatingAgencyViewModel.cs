using Application.Models;
using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Admin.AgecyManagement
{
  public class CreateAgencyViewModel
  {
    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    public string Name { get; set; }

    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    [StringLength(200, MinimumLength = 10, ErrorMessage = "آدرس حدقل باید 8 کاراکتر باشد")]
    public string Address { get; set; }

    [MaxLength(32)]
    public string? EconomicNo { get; set; }

    [MaxLength(32)]
    public string? RegistrationNo { get; set; }

    [MaxLength(20)]
    public string? NationalId { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    [RegularExpression(@"^09[0-9]{9}$", ErrorMessage = "لطفا شماره تلفن را صحیح و همراه صفر وارد کنید")]
    public string AdminMobile { get; set; }

    /// <summary>Seller or Organization. Only Organization gets official invoices.</summary>
    public AgencyPanelType PanelType { get; set; } = AgencyPanelType.Seller;

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    [Range(0, 10, ErrorMessage = "کمیسیون می‌تواند بین 0 تا 10 درصد باشد")]
    public int Commission { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    public string Username { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "رمز عبور باید حداقل 8 کارکتر باشد")]
    public string Password { get; set; }

    [Required(ErrorMessage = "وارد کردن این مورد الزامی است")]
    [Compare("Password", ErrorMessage = "تکرار رمز با رمز همخوانی ندارد")]
    public string RetypePassword { get; set; }
  }
}
