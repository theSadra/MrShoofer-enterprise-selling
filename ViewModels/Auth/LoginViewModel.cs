using System.ComponentModel.DataAnnotations;

namespace Application.ViewModels.Auth
{
  public class LoginViewModel
  {
    public string Username { get; set; }
    public string Password { get; set; }
    public bool RemmemberMe { get; set; }
  }
}
