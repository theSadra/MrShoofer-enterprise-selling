using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Application.Controllers
{
  [Authorize]
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


      var trip = await apiclient.GetTripInfo(tripcode);

      ViewBag.trip = trip;


      return View();
    }
  }
}
