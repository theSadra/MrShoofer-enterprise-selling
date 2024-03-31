using Microsoft.AspNetCore.Mvc;

namespace AspnetCoreMvcStarter.Controllers
{
  public class AuthController : Controller
  {
    public IActionResult Login()
    {
      return View();
    }
  }
}
