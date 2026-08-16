using Application.Data;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.Services.Payment;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Application.Models;
using Newtonsoft.Json;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  // Guests can access most actions - authorization checked per-action
  public class ReserveController : Controller
  {

    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;
    private readonly CustomerServiceSmsSender customerSmsSender;
    private readonly IConfiguration configuration;
    private readonly IPaymentService _payment;
    private Agency agency;


    public ReserveController(MrShooferAPIClient apiclient, UserManager<IdentityUser> usermanager, AppDbContext context, CustomerServiceSmsSender smssender, IConfiguration configuration, IPaymentService payment)
    {
      this.configuration = configuration;
      customerSmsSender = smssender;
      this.context = context;
      _userManager = usermanager;
      this.apiclient = apiclient;
      _payment = payment;
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

      // Getting agancy account balance from ORS - only if authenticated
      if (User.Identity.IsAuthenticated)
      {
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
      }
      else
      {
        // Guest user - show they need to login
        ViewBag.agancy_balance = 0;
        ViewBag.canbuy = false;
        ViewBag.isGuest = true;
      }

      ViewBag.trip = trip;

      // Check if there's saved form data from TempData (after login redirect)
      if (TempData.ContainsKey("SavedReserveData"))
      {
        var savedDataJson = TempData["SavedReserveData"]?.ToString();
        if (!string.IsNullOrEmpty(savedDataJson))
        {
          try
          {
            var savedData = JsonConvert.DeserializeObject<ReserveInfoViewModel>(savedDataJson);

            // IMPORTANT: Remove the data from TempData after reading it
            // This ensures it's only used once and won't persist on page refresh
            TempData.Remove("SavedReserveData");

            // Pass the saved data to the view
            return View(savedData);
          }
          catch
          {
            // If deserialization fails, remove the corrupted data
            TempData.Remove("SavedReserveData");
          }
        }
      }

      return View();
    }


    [HttpPost]
    public async Task<IActionResult> Reservetrip(ReserveInfoViewModel viewmodel)
    {
      // Check if user is authenticated before allowing reservation
      if (!User.Identity.IsAuthenticated)
      {
        // Save the form data in TempData before redirecting to login
        // TempData will survive ONE redirect - perfect for our use case
        TempData["SavedReserveData"] = JsonConvert.SerializeObject(viewmodel);
        // NOTE: We don't use TempData.Keep() because we want it to be used only once

        // Return JSON to trigger modal on client side
        var returnUrl = Url.Action("Reservetrip", "Reserve", new { tripcode = viewmodel.TripCode, area = "AgencyArea" });
        return Json(new { requiresAuth = true, returnUrl = returnUrl });
      }

      if (!ModelState.IsValid)
      {
        ViewData["ReservationId"] = viewmodel.TripCode;

        var invalidTrip = await apiclient.GetTripInfo(viewmodel.TripCode);
        ViewBag.trip = invalidTrip;

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
          var invalidViewAgencyBalance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());
          ViewBag.agancy_balance = invalidViewAgencyBalance;
          ViewBag.canbuy = invalidViewAgencyBalance >= invalidTrip.afterdiscticketprice;
        }
        else
        {
          ViewBag.agancy_balance = 0;
          ViewBag.canbuy = false;
          ViewBag.isGuest = true;
        }

        return View(viewmodel);
      }

      // Get trip info — ZarinPal is always available, so do not block on agency credit.
      var trip = await apiclient.GetTripInfo(viewmodel.TripCode);
      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());

      ViewBag.agancy_balance = agancy_balance;
      ViewBag.canbuy = agancy_balance >= trip.afterdiscticketprice;

      ViewBag.agancy = agency;
      ViewBag.trip = trip;
      ViewBag.reserveviewmodel = viewmodel;

      return View("ConfirmInfo");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmInfo(ConfirmInfoViewModel viewModel)
    {
      if (!User.Identity.IsAuthenticated)
      {
        return Json(new { requiresAuth = true, returnUrl = Url.Action("Reservetrip", "Reserve", new { tripcode = viewModel.TripCode }) });
      }

      var trip = await apiclient.GetTripInfo(viewModel.TripCode);
      var method = (viewModel.PaymentMethod ?? "zarinpal").Trim().ToLowerInvariant();

      if (method == "zarinpal")
        return await StartZarinpalTicketPayment(viewModel, trip);

      var agencyBalance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());
      if (agencyBalance < trip.afterdiscticketprice)
      {
        TempData["ErrorMessage"] = "موجودی حساب آژانس کافی نیست. از درگاه زرین‌پال استفاده کنید.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      return await IssueTicketFromOrs(viewModel, trip);
    }

    private async Task<IActionResult> StartZarinpalTicketPayment(ConfirmInfoViewModel viewModel, SearchedTrip trip)
    {
      if (agency == null)
      {
        TempData["ErrorMessage"] = "حساب آژانس یافت نشد.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      var amountToman = trip.afterdiscticketprice;
      var callback = configuration["Zarinpal:TicketCallbackUrl"];
      if (string.IsNullOrWhiteSpace(callback) && Request.Host.HasValue)
        callback = $"{Request.Scheme}://{Request.Host}/Payments/ZarinpalTicketCallback";
      if (string.IsNullOrWhiteSpace(callback))
        callback = configuration["Zarinpal:CallbackUrl"]?.Replace("ZarinpalCallback", "ZarinpalTicketCallback")
                   ?? "http://localhost:5055/Payments/ZarinpalTicketCallback";

      var (success, authority, message) = await _payment.RequestPaymentAsync(
        amountToman * 10,
        description: $"خرید بلیط سواری {trip.originCityName} به {trip.destinationCityName}",
        mobile: viewModel.Numberphone,
        callbackUrl: callback);

      if (!success)
      {
        TempData["ErrorMessage"] = message;
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      context.ZarinpalTicketPayments.Add(new ZarinpalTicketPayment
      {
        Authority = authority,
        AmountToman = amountToman,
        Status = "Pending",
        CreatedAt = DateTime.Now,
        TripCode = viewModel.TripCode,
        Firstname = viewModel.Firstname,
        Lastname = viewModel.Lastname,
        Numberphone = viewModel.Numberphone,
        Nacode = viewModel.Nacode,
        Gender = viewModel.Gender,
        Agency = agency
      });
      await context.SaveChangesAsync();

      return Redirect(_payment.GetPaymentGatewayUrl(authority));
    }

    private async Task<IActionResult> IssueTicketFromOrs(ConfirmInfoViewModel viewModel, SearchedTrip trip)
    {
      TicketTempReserveRequestModel tempreserve_viewodel = new TicketTempReserveRequestModel()
      {
        isPrivate = true,
        tripCode = viewModel.TripCode
      };

      var reservecode = await apiclient.ReserveTicketTemporarirly(tempreserve_viewodel);

      ConfirmReserveRequestModel confirmreserve_viewmodel = new ConfirmReserveRequestModel()
      {
        passengerFirstName = viewModel.Firstname,
        passengerLastName = viewModel.Lastname,
        reservationCode = reservecode,
        passengerNationalCode = viewModel.Nacode,
        passengerNumberPhone = viewModel.Numberphone
      };

      TicketConfirmationResponse reserve_response;
      try
      {
        reserve_response = await apiclient.ConfirmReserve(confirmreserve_viewmodel);
      }
      catch (Exception)
      {
        return RedirectToAction("Index", "Home");
      }

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

      var identity_user = await _userManager.GetUserAsync(User);
      var agancy = context.Agencies.Where(a => a.IdentityUser == identity_user).FirstOrDefault();
      newticket.Agency = agancy;

      context.Tickets.Add(newticket);
      await context.SaveChangesAsync();

      try
      {
        await customerSmsSender.SendCustomerTicket_issued(newticket.Firstname, newticket.Lastname, newticket.TicketCode, newticket.TicketCode, newticket.PhoneNumber);
      }
      catch
      {
      }

      return RedirectToAction("ReserveConfirmed", new { ticketcode = newticket.TicketCode });
    }

    [Authorize]
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

      string tokenToUse = null;

      // Use agency token if authenticated, otherwise use guest/default token
      if (User.Identity.IsAuthenticated)
      {
        var identityUser = _userManager.GetUserAsync(User).Result;
        agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);

        if (agency != null && !string.IsNullOrWhiteSpace(agency.ORSAPI_token))
        {
          tokenToUse = agency.ORSAPI_token;
        }
      }

      // Fallback to guest token from configuration
      if (string.IsNullOrWhiteSpace(tokenToUse))
      {
        tokenToUse = configuration["MrShoofer:SellerToken"];
      }

      if (!string.IsNullOrWhiteSpace(tokenToUse))
      {
        apiclient.SetSellerApiKey(tokenToUse);
      }
    }
  }
}
