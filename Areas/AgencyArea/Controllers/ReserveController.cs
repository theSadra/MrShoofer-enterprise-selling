using Application.Data;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.Services.Payment;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Application.Models;
using Application.Utilities;
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
    private int? _commissionPercent;


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
        var payable = PayableTicketPrice(trip);

        ViewBag.agancy_balance = agancy_balance;
        ViewBag.payablePrice = payable;
        ViewBag.canbuy = agancy_balance >= payable;
        ViewBag.canPayHybrid = agancy_balance > 0 && agancy_balance < payable;
      }
      else
      {
        // Guest user — same price display as 0% commission (قبل/بعد تخفیف, full list payable).
        ViewBag.agancy_balance = 0;
        ViewBag.payablePrice = PayableTicketPrice(trip);
        ViewBag.listPrice = trip.afterdiscticketprice;
        ViewBag.commissionPercent = 0;
        ViewBag.canbuy = false;
        ViewBag.canPayHybrid = false;
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
        viewmodel.Companions = NormalizeCompanions(viewmodel.Companions);
        TempData["SavedReserveData"] = JsonConvert.SerializeObject(viewmodel);
        var returnUrl = Url.Action("Reservetrip", "Reserve", new { tripcode = viewmodel.TripCode, area = "AgencyArea" });
        return Json(new { requiresAuth = true, returnUrl = returnUrl });
      }

      viewmodel.Companions = NormalizeCompanions(viewmodel.Companions);
      ClearCompanionModelStateErrors();
      if (!ValidateCompanions(viewmodel.Companions, out var companionError))
      {
        ModelState.AddModelError(nameof(viewmodel.Companions), companionError);
      }

      if (!ModelState.IsValid)
      {
        ViewData["ReservationId"] = viewmodel.TripCode;

        var invalidTrip = await apiclient.GetTripInfo(viewmodel.TripCode);
        ViewBag.trip = invalidTrip;

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
          var invalidViewAgencyBalance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());
          var invalidPayable = PayableTicketPrice(invalidTrip);
          ViewBag.agancy_balance = invalidViewAgencyBalance;
          ViewBag.payablePrice = invalidPayable;
          ViewBag.canbuy = invalidViewAgencyBalance >= invalidPayable;
          ViewBag.canPayHybrid = invalidViewAgencyBalance > 0 && invalidViewAgencyBalance < invalidPayable;
        }
        else
        {
          ViewBag.agancy_balance = 0;
          ViewBag.payablePrice = PayableTicketPrice(invalidTrip);
          ViewBag.listPrice = invalidTrip.afterdiscticketprice;
          ViewBag.commissionPercent = 0;
          ViewBag.canbuy = false;
          ViewBag.canPayHybrid = false;
          ViewBag.isGuest = true;
        }

        return View(viewmodel);
      }

      // Get trip info — ZarinPal is always available, so do not block on agency credit.
      var trip = await apiclient.GetTripInfo(viewmodel.TripCode);
      var agancy_balance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());
      var payable = PayableTicketPrice(trip);

      ViewBag.agancy_balance = agancy_balance;
      ViewBag.canbuy = agancy_balance >= payable;
      ViewBag.canPayHybrid = agancy_balance > 0 && agancy_balance < payable;
      ViewBag.payablePrice = payable;
      ViewBag.listPrice = trip.afterdiscticketprice;

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

      if (!ModelState.IsValid)
      {
        TempData["ErrorMessage"] = "اطلاعات مسافر ناقص است.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      var trip = await apiclient.GetTripInfo(viewModel.TripCode);
      var method = (viewModel.PaymentMethod ?? "zarinpal").Trim().ToLowerInvariant();
      var agencyBalance = (int)Convert.ToDouble(await apiclient.GetAccountBalance());
      var ticketPrice = PayableTicketPrice(trip);

      if (method == "zarinpal")
        return await StartZarinpalTicketPayment(viewModel, trip, walletApplied: 0, gatewayAmount: ticketPrice);

      if (method == "hybrid")
      {
        if (agencyBalance <= 0 || agencyBalance >= ticketPrice)
        {
          TempData["ErrorMessage"] = "برای پرداخت ترکیبی، موجودی کیف پول باید کمتر از قیمت بلیط و بیشتر از صفر باشد.";
          return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
        }

        var remainder = ticketPrice - agencyBalance;
        return await StartZarinpalTicketPayment(viewModel, trip, walletApplied: agencyBalance, gatewayAmount: remainder);
      }

      if (agencyBalance < ticketPrice)
      {
        TempData["ErrorMessage"] = "موجودی حساب آژانس کافی نیست. از درگاه یا پرداخت ترکیبی استفاده کنید.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      return await IssueTicketFromOrs(viewModel, trip);
    }

    private async Task<IActionResult> StartZarinpalTicketPayment(
      ConfirmInfoViewModel viewModel,
      SearchedTrip trip,
      int walletApplied,
      int gatewayAmount)
    {
      if (agency == null)
      {
        TempData["ErrorMessage"] = "حساب آژانس یافت نشد.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      if (gatewayAmount <= 0)
      {
        TempData["ErrorMessage"] = "مبلغ درگاه نامعتبر است.";
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      var ticketPrice = PayableTicketPrice(trip);
      var callback = configuration["Zarinpal:TicketCallbackUrl"];
      if (string.IsNullOrWhiteSpace(callback) && Request.Host.HasValue)
        callback = $"{Request.Scheme}://{Request.Host}/Payments/ZarinpalTicketCallback";
      if (string.IsNullOrWhiteSpace(callback))
        callback = configuration["Zarinpal:CallbackUrl"]?.Replace("ZarinpalCallback", "ZarinpalTicketCallback")
                   ?? "http://localhost:5055/Payments/ZarinpalTicketCallback";

      var description = walletApplied > 0
        ? $"خرید بلیط سواری {trip.originCityName} به {trip.destinationCityName} (باقیمانده پس از کیف پول)"
        : $"خرید بلیط سواری {trip.originCityName} به {trip.destinationCityName}";

      var (success, authority, message) = await _payment.RequestPaymentAsync(
        gatewayAmount * 10,
        description: description,
        mobile: viewModel.Numberphone,
        callbackUrl: callback);

      if (!success)
      {
        TempData["ErrorMessage"] = message;
        return RedirectToAction("Reservetrip", new { tripcode = viewModel.TripCode });
      }

      var employeeId = await ResolveAgencyEmployeeIdAsync(viewModel.EmployeeId, viewModel.Nacode, viewModel.Numberphone);
      var companionsJson = NormalizeCompanionsJson(viewModel.CompanionsJson);
      var companionsDescription = BuildCompanionsDescription(companionsJson);

      context.ZarinpalTicketPayments.Add(new ZarinpalTicketPayment
      {
        Authority = authority,
        AmountToman = gatewayAmount,
        WalletAppliedToman = walletApplied,
        TicketPriceToman = ticketPrice,
        Status = "Pending",
        CreatedAt = DateTime.Now,
        TripCode = viewModel.TripCode,
        Firstname = viewModel.Firstname,
        Lastname = viewModel.Lastname,
        Numberphone = viewModel.Numberphone,
        Nacode = viewModel.Nacode,
        Gender = viewModel.Gender,
        CompanyName = companionsDescription,
        AgencyEmployeeId = employeeId,
        CompanionsJson = companionsJson,
        Agency = agency
      });
      await context.SaveChangesAsync();

      var enterUrl = $"{Request.Scheme}://{Request.Host}{PaymentGatewayRedirect.BuildEnterGatewayPath(authority)}";
      var (html, contentType) = PaymentGatewayRedirect.BuildRedirectPage(enterUrl);
      return Content(html, contentType);
    }

    private async Task<IActionResult> IssueTicketFromOrs(ConfirmInfoViewModel viewModel, SearchedTrip trip)
    {
      TicketTempReserveRequestModel tempreserve_viewodel = new TicketTempReserveRequestModel()
      {
        isPrivate = true,
        tripCode = viewModel.TripCode
      };

      var reservecode = await apiclient.ReserveTicketTemporarirly(tempreserve_viewodel);
      var companionsDescription = BuildCompanionsDescription(viewModel.CompanionsJson);

      ConfirmReserveRequestModel confirmreserve_viewmodel = new ConfirmReserveRequestModel()
      {
        passengerFirstName = viewModel.Firstname,
        passengerLastName = viewModel.Lastname,
        reservationCode = reservecode,
        passengerNationalCode = viewModel.Nacode,
        passengerNumberPhone = viewModel.Numberphone,
        passengerCompanyName = companionsDescription
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

      var employeeId = await ResolveAgencyEmployeeIdAsync(viewModel.EmployeeId, viewModel.Nacode, viewModel.Numberphone);

      Ticket newticket = new Ticket()
      {
        Firstname = viewModel.Firstname,
        Lastname = viewModel.Lastname,
        PhoneNumber = viewModel.Numberphone,
        NaCode = viewModel.Nacode,
        CompanyName = companionsDescription,
        TicketFinalPrice = reserve_response.paid_total_fee_tomans,
        Gender = viewModel.Gender,
        TicketOriginalPrice = trip.afterdiscticketprice,
        TripOrigin = trip.originCityName,
        TripDestination = trip.destinationCityName,
        RegisteredAt = DateTime.Now,
        TicketCode = reserve_response.ticketCode,
        Tripcode = trip.tripPlanCode,
        ServiceName = trip.taxiSupervisorName,
        CarName = trip.carModelName,
        AgencyEmployeeId = employeeId
      };

      var identity_user = await _userManager.GetUserAsync(User);
      var agancy = context.Agencies.Where(a => a.IdentityUser == identity_user).FirstOrDefault();
      newticket.Agency = agancy;

      context.Tickets.Add(newticket);
      await context.SaveChangesAsync();
      await AttachCompanionsAsync(newticket.Id, viewModel.CompanionsJson);

      try
      {
        await customerSmsSender.SendCustomerTicket_issued(newticket.Firstname, newticket.Lastname, newticket.TicketCode, newticket.TicketCode, newticket.PhoneNumber);
      }
      catch
      {
      }

      return RedirectToAction("ReserveConfirmed", new { ticketcode = newticket.TicketCode });
    }

    private async Task AttachCompanionsAsync(int ticketId, string? companionsJson)
    {
      var companions = DeserializeCompanions(companionsJson);
      if (companions.Count == 0) return;

      var order = 1;
      foreach (var c in companions)
      {
        context.TicketCompanions.Add(new TicketCompanion
        {
          TicketId = ticketId,
          SortOrder = order++,
          Firstname = c.Firstname.Trim(),
          Lastname = c.Lastname.Trim(),
          Gender = c.Gender.Trim(),
          NaCode = c.NaCode.Trim(),
          PhoneNumber = string.IsNullOrWhiteSpace(c.PhoneNumber) ? null : c.PhoneNumber.Trim(),
          AgencyEmployeeId = await ResolveAgencyEmployeeIdAsync(c.EmployeeId, c.NaCode, c.PhoneNumber)
        });
      }

      await context.SaveChangesAsync();
    }

    private void ClearCompanionModelStateErrors()
    {
      foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Companions", StringComparison.OrdinalIgnoreCase)).ToList())
        ModelState.Remove(key);
    }

    private static List<CompanionPassengerViewModel> NormalizeCompanions(IEnumerable<CompanionPassengerViewModel>? items)
    {
      return (items ?? Enumerable.Empty<CompanionPassengerViewModel>())
        .Where(c =>
          !string.IsNullOrWhiteSpace(c.Firstname) ||
          !string.IsNullOrWhiteSpace(c.Lastname) ||
          !string.IsNullOrWhiteSpace(c.NaCode) ||
          !string.IsNullOrWhiteSpace(c.PhoneNumber) ||
          !string.IsNullOrWhiteSpace(c.Gender))
        .Select(c => new CompanionPassengerViewModel
        {
          Firstname = c.Firstname?.Trim() ?? "",
          Lastname = c.Lastname?.Trim() ?? "",
          Gender = c.Gender?.Trim() ?? "",
          NaCode = c.NaCode?.Trim() ?? "",
          PhoneNumber = string.IsNullOrWhiteSpace(c.PhoneNumber) ? null : c.PhoneNumber.Trim(),
          EmployeeId = c.EmployeeId
        })
        .Take(2)
        .ToList();
    }

    private static bool ValidateCompanions(List<CompanionPassengerViewModel> companions, out string error)
    {
      error = "";
      foreach (var c in companions)
      {
        if (string.IsNullOrWhiteSpace(c.Firstname) || string.IsNullOrWhiteSpace(c.Lastname) ||
            string.IsNullOrWhiteSpace(c.Gender) || string.IsNullOrWhiteSpace(c.NaCode))
        {
          error = "اطلاعات همراهان ناقص است (نام، نام‌خانوادگی، جنسیت و کد ملی الزامی است).";
          return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(c.NaCode, @"^\d{10}$"))
        {
          error = "کد ملی همراه باید دقیقاً ۱۰ رقم باشد.";
          return false;
        }

        if (!string.IsNullOrWhiteSpace(c.PhoneNumber) &&
            !System.Text.RegularExpressions.Regex.IsMatch(c.PhoneNumber, @"^((0?9)|(\+?989))\d{9}$"))
        {
          error = "شماره موبایل همراه معتبر نیست.";
          return false;
        }
      }

      return true;
    }

    private static string? NormalizeCompanionsJson(string? json)
    {
      var list = DeserializeCompanions(json);
      if (list.Count == 0) return null;
      return JsonConvert.SerializeObject(list);
    }

    /// <summary>
    /// ORS confirm only has passengerCompanyName as free-text — put companion summary there.
    /// Example: همراهان: آقا رضا کریمی (کدملی ۱۲۳۴۵۶۷۸۹۱، موبایل ۰۹۱۲۱۱۱۳۳۴۴)
    /// </summary>
    private static string BuildCompanionsDescription(string? companionsJson)
    {
      var companions = DeserializeCompanions(companionsJson);
      if (companions.Count == 0) return "";

      var parts = new List<string>();
      foreach (var c in companions)
      {
        var title = string.Equals(c.Gender, "female", StringComparison.OrdinalIgnoreCase) ? "خانم" : "آقا";
        var name = $"{title} {c.Firstname.Trim()} {c.Lastname.Trim()}".Trim();
        var bits = new List<string> { $"کدملی {c.NaCode.Trim()}" };
        if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
          bits.Add($"موبایل {c.PhoneNumber.Trim()}");
        parts.Add($"{name} ({string.Join("، ", bits)})");
      }

      return "همراهان: " + string.Join("؛ ", parts);
    }

    private static List<CompanionPassengerViewModel> DeserializeCompanions(string? json)
    {
      if (string.IsNullOrWhiteSpace(json)) return new List<CompanionPassengerViewModel>();
      try
      {
        var parsed = JsonConvert.DeserializeObject<List<CompanionPassengerViewModel>>(json);
        return NormalizeCompanions(parsed);
      }
      catch
      {
        return new List<CompanionPassengerViewModel>();
      }
    }

    private async Task<int?> ResolveAgencyEmployeeIdAsync(int? employeeId, string? naCode = null, string? phoneNumber = null)
    {
      if (agency == null)
        return null;

      if (employeeId is > 0)
      {
        var belongs = await context.AgencyEmployees
          .AnyAsync(e => e.Id == employeeId.Value && e.AgencyId == agency.Id);
        if (belongs) return employeeId;
      }

      var na = (naCode ?? "").Trim();
      if (!string.IsNullOrEmpty(na))
      {
        var byNa = await context.AgencyEmployees
          .Where(e => e.AgencyId == agency.Id && e.NaCode == na)
          .Select(e => (int?)e.Id)
          .FirstOrDefaultAsync();
        if (byNa != null) return byNa;
      }

      var phone = (phoneNumber ?? "").Trim();
      if (!string.IsNullOrEmpty(phone))
      {
        var byPhone = await context.AgencyEmployees
          .Where(e => e.AgencyId == agency.Id && e.PhoneNumber == phone)
          .Select(e => (int?)e.Id)
          .FirstOrDefaultAsync();
        if (byPhone != null) return byPhone;
      }

      return null;
    }

    [AllowAnonymous]
    public async Task<IActionResult> ReserveConfirmed(string ticketcode)
    {
      if (string.IsNullOrWhiteSpace(ticketcode))
        return RedirectToAction("Index", "TicketInfo");

      var ticket = context.Tickets.Where(t => t.TicketCode == ticketcode).FirstOrDefault();
      if (ticket == null)
      {
        TempData["ErrorMessage"] = "بلیط یافت نشد.";
        return RedirectToAction("Index", "TicketInfo");
      }

      ViewBag.trip = await apiclient.GetTripInfo(ticket.Tripcode);
      ViewBag.ticket = ticket;
      var commissionPercent = GetCommissionPercent();
      var listPrice = ((SearchedTrip)ViewBag.trip).afterdiscticketprice;
      ViewBag.payablePrice = AgencyCommissionPricing.NetPayableTomans(listPrice, commissionPercent);
      ViewBag.listPrice = listPrice;
      ViewBag.commissionPercent = commissionPercent;
      // Only agencies with commission may override passenger-facing display prices.
      ViewBag.canEditDisplayPrices = commissionPercent > 0;

      return View();
    }

    /// <summary>
    /// Agency override of passenger-facing ticket prices (display/print). Unlimited amounts.
    /// Always returns JSON (no cookie-auth HTML redirect) so the confirmation modal can parse errors.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateTicketDisplayPrices([FromBody] UpdateTicketDisplayPricesRequest? request)
    {
      try
      {
        if (request == null || string.IsNullOrWhiteSpace(request.TicketCode))
          return Json(new { success = false, message = "کد بلیط نامعتبر است." });

        if (request.OriginalPrice < 0 || request.FinalPrice < 0)
          return Json(new { success = false, message = "مبالغ نمی‌توانند منفی باشند." });

        if (User?.Identity?.IsAuthenticated != true)
          return Json(new { success = false, message = "برای تغییر قیمت وارد حساب آژانس شوید." });

        if (agency == null)
        {
          var identityUser = await _userManager.GetUserAsync(User);
          if (identityUser != null)
          {
            agency = context.Agencies.FirstOrDefault(a => a.IdentityUserId == identityUser.Id)
                     ?? context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);
          }
        }

        if (agency == null)
          return Json(new { success = false, message = "آژانس یافت نشد." });

        if (GetCommissionPercent() <= 0)
          return Json(new { success = false, message = "تغییر قیمت نمایشی فقط برای آژانس‌های دارای کمیسیون فعال است." });

        var ticketCode = request.TicketCode.Trim();
        var ticket = await context.Tickets
          .FirstOrDefaultAsync(t =>
            t.TicketCode == ticketCode &&
            EF.Property<int>(t, "AgencyId") == agency.Id);

        if (ticket == null)
          return Json(new { success = false, message = "بلیط یافت نشد یا متعلق به این آژانس نیست." });

        ticket.TicketOriginalPrice = request.OriginalPrice;
        ticket.TicketFinalPrice = request.FinalPrice;
        await context.SaveChangesAsync();

        // Display-only override for agency ticket print/receipt — not synced to ORS.

        return Json(new
        {
          success = true,
          originalPrice = ticket.TicketOriginalPrice,
          finalPrice = ticket.TicketFinalPrice
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, message = "ذخیره قیمت ناموفق بود: " + ex.Message });
      }
    }

    public class UpdateTicketDisplayPricesRequest
    {
      public string TicketCode { get; set; } = "";
      public int OriginalPrice { get; set; }
      public int FinalPrice { get; set; }
    }

    private int PayableTicketPrice(SearchedTrip trip) =>
      AgencyCommissionPricing.NetPayableTomans(trip.afterdiscticketprice, GetCommissionPercent());

    /// <summary>
    /// Live ORS baseCommission when available; otherwise local Agency.Commission.
    /// Unauthenticated guests always 0% (قبل/بعد تخفیف UI, full list price payable).
    /// </summary>
    private int GetCommissionPercent()
    {
      if (_commissionPercent.HasValue) return _commissionPercent.Value;
      if (User?.Identity?.IsAuthenticated != true || agency == null)
      {
        _commissionPercent = 0;
        return 0;
      }

      try
      {
        var orsInfo = apiclient.GetMyAgencyInfoAsync().GetAwaiter().GetResult();
        if (orsInfo != null)
        {
          var live = (int)Math.Round(orsInfo.BaseCommission, MidpointRounding.AwayFromZero);
          if (live < 0) live = 0;
          if (live > 100) live = 100;
          if (agency.Commission != live)
          {
            agency.Commission = live;
            try { context.SaveChanges(); } catch { /* non-fatal */ }
          }
          _commissionPercent = live;
          return live;
        }
      }
      catch { /* fall back */ }

      _commissionPercent = agency.Commission;
      return agency.Commission;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      string tokenToUse = null;

      // Use agency token if authenticated, otherwise use guest/default token
      if (User.Identity.IsAuthenticated)
      {
        var identityUser = _userManager.GetUserAsync(User).Result;
        if (identityUser != null)
        {
          agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUserId == identityUser.Id)
                   ?? this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);
        }

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
