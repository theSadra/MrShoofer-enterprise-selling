using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Application.Controllers
{
  public class ReserveController : Controller
  {
    private readonly MrShooferAPIClient apiclient;

    public ReserveController(MrShooferAPIClient apiclient)
    {
      this.apiclient = apiclient;
    }

    public IActionResult Index()
    {
      return View();
    }

    public async Task<IActionResult> Reservetrip(string tripcode)
    {
      ViewData["ReservationId"] = tripcode;


      var result = await apiclient.GetTripInfo(tripcode);


      return View();
    }
  }
}
