using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace Application.ViewModels.Reserve
{
  public class ConfirmInfoViewModel
  {
    public string TripCode { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string Numberphone { get; set; }
    public string Nacode { get; set; }
    public string Gender { set; get; }
  }
}
