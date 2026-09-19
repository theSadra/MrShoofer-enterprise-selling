using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Employees
{
  public class AgencyEmployeeFormViewModel
  {
    public int? Id { get; set; }

    [Required(ErrorMessage = "نام را وارد کنید")]
    public string Firstname { get; set; } = "";

    [Required(ErrorMessage = "نام خانوادگی را وارد کنید")]
    public string Lastname { get; set; } = "";

    [Required(ErrorMessage = "جنسیت را انتخاب کنید")]
    public string Gender { get; set; } = "";

    [Required(ErrorMessage = "کد ملی را وارد کنید")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی باید دقیقاً 10 رقم باشد")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید دقیقاً 10 رقم باشد")]
    public string NaCode { get; set; } = "";

    [Required(ErrorMessage = "شماره تلفن را وارد کنید")]
    [RegularExpression(@"^((0?9)|(\+?989))\d{9}$", ErrorMessage = "شماره تلفن را صحیح وارد کنید")]
    public string PhoneNumber { get; set; } = "";

    [EmailAddress(ErrorMessage = "لطفا یک ایمیل معتبر وارد کنید")]
    public string? Email { get; set; }
  }
}
