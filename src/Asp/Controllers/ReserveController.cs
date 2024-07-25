using Application.Services.MrShooferORS;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using static System.Runtime.CompilerServices.RuntimeHelpers;

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
      if (string.IsNullOrEmpty(tripcode))
        return BadRequest();


      ViewData["ReservationId"] = tripcode;

      var trip = await apiclient.GetTripInfo(tripcode);

      ViewBag.trip = trip;


      return View();
    }


    [HttpPost]
    public async Task<IActionResult> Reservetrip(ReserveInfoViewModel viewmodel)
    {
      if (!ModelState.IsValid)
      {
        return RedirectToAction("Reservetrip");
      }


      var trip = await apiclient.GetTripInfo(viewmodel.TripCode);




      ViewBag.trip = trip;
      ViewBag.reserveviewmodel = viewmodel;
        
      return View("ConfirmInfo");
    }


    public IActionResult ConfirmInfo(string tripcode)
    {
      if (string.IsNullOrEmpty(tripcode))
        return BadRequest();


      ViewData["ReservationId"] = tripcode;

      var trip = apiclient.GetTripInfo(tripcode).Result;

      ViewBag.trip = trip;


      return View("ConfirmInfo");
    }
  }
}
