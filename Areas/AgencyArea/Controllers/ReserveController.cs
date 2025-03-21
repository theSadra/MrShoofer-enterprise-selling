using Application.Data;
using Application.Services;
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
using Application.Models;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize]
  public class ReserveController : Controller
  {

    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;
    private readonly CustomerServiceSmsSender customerSmsSender;
    private readonly IConfiguration configuration;
    private Agency agency;


    public ReserveController(MrShooferAPIClient apiclient, UserManager<IdentityUser> usermanager, AppDbContext context, CustomerServiceSmsSender smssender, IConfiguration configuration)
    {
      this.configuration = configuration;
      customerSmsSender = smssender;
      this.context = context;
      _userManager = usermanager;
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

      ViewBag.agancy = agency;
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


      TicketConfirmationResponse reserve_response = null;

        try
        {
           reserve_response = await apiclient.ConfirmReserve(confirmreserve_viewmodel);
        }
        catch (Exception e)
        {
          return RedirectToAction("Index", "Home");
        }


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
          Tripcode = trip.tripPlanCode,
          ServiceName = trip.taxiSupervisorName,
          CarName = trip.carModelName
        };

        // Registering to database


        var identity_user = await _userManager.GetUserAsync(User);

        var agancy = context.Agencies.Where(a => a.IdentityUser == identity_user).FirstOrDefault();
        newticket.Agency = agancy;


        context.Tickets.Add(newticket);

        await context.SaveChangesAsync();



        //Sending SMS for customer


        var service_url = configuration["serivce_url"];
        var trip_link = newticket.TicketCode;


      try
      {

        await customerSmsSender.SendCustomerTicket_issued(newticket.Firstname, newticket.Lastname, newticket.TicketCode, trip_link, newticket.PhoneNumber);
      }
      catch
      {

      }



        return RedirectToAction("ReserveConfirmed", new { ticketcode = newticket.TicketCode });
      }





    private async Task DoConfirmResreve()
    {

    }



    public async Task<IActionResult> ReserveConfirmed(string ticketcode)
    {
      var ticket = context.Tickets.Where(t => t.TicketCode == ticketcode).FirstOrDefault();
      ViewBag.trip = await apiclient.GetTripInfo(ticket.Tripcode);
      ViewBag.ticket = ticket;


      return View();
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      var identityUser = _userManager.GetUserAsync(User).Result;
      agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);


      apiclient.SetSellerApiKey(agency.ORSAPI_token);
    }
  }
}
