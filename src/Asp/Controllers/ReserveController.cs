using Application.Data;
using Application.Services.MrShooferORS;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
    private Agency agency;


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

      if (string.IsNullOrEmpty(tripcode))
        return BadRequest();


      ViewData["ReservationId"] = tripcode;

      var trip = await apiclient.GetTripInfo(tripcode);

      // Getting agancy account balance from ORS
      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());

      ViewBag.agancy_balance = agancy_balance;


      if (agancy_balance >= trip.afterdiscticketprice)
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

      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());

      ViewBag.agancy_balance = agancy_balance;

      ViewBag.agancy = this.agency;
      ViewBag.trip = trip;
      ViewBag.reserveviewmodel = viewmodel;

      return View("ConfirmInfo");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmInfo(ConfirmInfoViewModel viewModel)
    {
      // Registering the ticket


      // Issuing ticket in ORS
      // TempReserve

      TicketTempReserveRequestModel tempreserve_viewodel = new TicketTempReserveRequestModel()
      {
        isPrivate = true,
        tripCode = viewModel.TripCode
      };

      var reservecode = await apiclient.ReserveTicketTemporarirly(tempreserve_viewodel);


      // final reserve

      ConfirmReserveRequestModel confirmreserve_viewmodel = new ConfirmReserveRequestModel()
      {
        passengerFirstName = viewModel.Firstname,
        passengerLastName = viewModel.Lastname,
        reservationCode = reservecode,
        passengerNationalCode = viewModel.Nacode,
        passengerNumberPhone = viewModel.Numberphone
      };

      var reserve_response = await apiclient.ConfirmReserve(confirmreserve_viewmodel);

      // Getting trip_info

      var trip = await apiclient.GetTripInfo(viewModel.TripCode);

      //Creating ticket object
      Ticket newticket = new Ticket()
      {
        Firstname = viewModel.Firstname,
        Lastname = viewModel.Lastname,
        PhoneNumber = viewModel.Numberphone,
        NaCode = viewModel.Nacode,
        TicketFinalPrice = reserve_response.paid_total_fee_tomans,
        Gender = viewModel.Gender,
        TicketOriginalPrice = trip.originalTicketprice,
        TripOrigin = trip.originCityName,
        TripDestination = trip.destinationCityName,
        RegisteredAt = DateTime.Now,
        TicketCode = reserve_response.ticketCode,
        Tripcode = trip.tripPlanCode
      };

      // Registering to database


      var identity_user = await _userManager.GetUserAsync(User);

      var agancy = context.Agencies.Where(a => a.IdentityUser == identity_user).FirstOrDefault();
      newticket.Agency = agancy;


      context.Tickets.Add(newticket);

      await context.SaveChangesAsync();


      return View();
    }




    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      var identityUser = _userManager.GetUserAsync(User).Result;
      this.agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);


      this.apiclient.SetSellerApiKey(this.agency.ORSAPI_token);
    }
  }
}
