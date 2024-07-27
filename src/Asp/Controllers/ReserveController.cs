using Application.Data;
using Application.Services.MrShooferORS;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Data.Entity;
using System.Diagnostics;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Application.Controllers
{
  [Authorize]
  public class ReserveController : Controller
  {

    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;



    public ReserveController(MrShooferAPIClient apiclient, UserManager<IdentityUser> usermanager, AppDbContext context)
    {
      this.context = context;
      this._userManager = usermanager;
      this.apiclient = apiclient;
    }

    public IActionResult Index()
    {
      return View();
    }


    public async Task<IActionResult> Reservetrip(string tripcode)
    {
      var identityUser = await _userManager.GetUserAsync(User);

      var agancy_user = context.Agencies.Where(a => a.IdentityUser == identityUser).FirstOrDefault();



      if (string.IsNullOrEmpty(tripcode))
        return BadRequest();


      ViewData["ReservationId"] = tripcode;

      var trip = await apiclient.GetTripInfo(tripcode);

      // Getting agancy account balance from ORS
      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());

      ViewBag.agancy_balance = agancy_balance;


      if(agancy_balance >= trip.afterdiscticketprice)
      {
        ViewBag.canbuy = true;
      }
      // Cannot submit the ticket
      else
      {
        ViewBag.canbuy = false;
      }

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
      var identity_user = await _userManager.GetUserAsync(User);

      // Getting agancy account balance from ORS
      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());

      ViewBag.agancy_balance = agancy_balance;

      ViewBag.agancy = context.Agencies.Where(a => a.IdentityUser == identity_user).FirstOrDefault();
      ViewBag.trip = trip;
      ViewBag.reserveviewmodel = viewmodel;
        
      return View("ConfirmInfo");
    }

    [HttpPost]
    public IActionResult ConfirmInfo(ConfirmInfoViewModel viewModel)
    {
      // Registering the ticket
      return View();
    }
  }
}
