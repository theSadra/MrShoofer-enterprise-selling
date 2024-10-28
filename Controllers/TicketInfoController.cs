using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers
{
  public class TicketInfoController : Controller
  {
    public IActionResult Index()
    {
      return View();
    }
  }
}
