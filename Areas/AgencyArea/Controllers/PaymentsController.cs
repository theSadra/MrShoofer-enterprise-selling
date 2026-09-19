using Application.Data;
using Application.Models;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.AgencyArea.Controllers
{

  [Area("AgencyArea")]
  [Authorize]
  public class PaymentsController : Controller
  {
    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IPaymentService _payment;
    private readonly MrShooferAPIClient _apiClient;
    private readonly ILogger<PaymentsController> _logger;
    private readonly CustomerServiceSmsSender _sms;

    public PaymentsController(
      AppDbContext dbContext,
      UserManager<IdentityUser> usermanager,
      IPaymentService payment,
      MrShooferAPIClient apiClient,
      ILogger<PaymentsController> logger,
      CustomerServiceSmsSender sms)
    {
      _context = dbContext;
      _userManager = usermanager;
      _payment = payment;
      _apiClient = apiClient;
      _logger = logger;
      _sms = sms;
    }

    [HttpPost("/Payments/ChargeRequest")]
    public async Task<IActionResult> ChargePaymentRequest(string amount, string method, string? message)
    {
      var identityUser = await _userManager.FindByNameAsync(User.Identity.Name);
      var agency = await _context.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.IdentityUser == identityUser);

      var chargeRequest = new ChargePaymentRequest()
      {
        Amout = amount.Replace(",", ""),
        PaymentMethod = method,
        Message = message,
        Agency = agency,
        RequestedOn = DateTime.Now
      };

      try
      {
        _context.Attach(agency);
        _context.ChargePaymentRequests.Add(chargeRequest);
        _context.SaveChanges();
        return Ok();
      }
      catch (Exception)
      {
        return BadRequest();
      }
    }

    [HttpPost("/Payments/ZarinpalCharge")]
    public async Task<IActionResult> InitiateZarinpalCharge(int amountToman)
    {
      if (amountToman < 1000)
        return BadRequest("حداقل مبلغ شارژ ۱۰۰۰ تومان است");

      var agency = await _context.Agencies
          .AsNoTracking()
          .Include(a => a.IdentityUser)
          .FirstOrDefaultAsync(a => a.IdentityUser.UserName == User.Identity!.Name);
      if (agency == null) return Unauthorized();

      int amountRials = amountToman * 10;

      var (success, authority, message) = await _payment.RequestPaymentAsync(
          amountRials,
          description: "شارژ حساب آژانس مسترشوفر",
          mobile: agency.AdminMobile);

      if (!success)
      {
        _logger.LogError("Zarinpal payment request failed for agency {AgencyId}: {Message}", agency.Id, message);
        TempData["PaymentError"] = message;
        return RedirectToAction("Index", "Agency");
      }

      var zarinpalRequest = new ZarinpalChargeRequest
      {
        Authority = authority,
        AmountToman = amountToman,
        Status = "Pending",
        CreatedAt = DateTime.Now,
        Agency = agency
      };

      _context.Attach(agency);
      _context.ZarinpalChargeRequests.Add(zarinpalRequest);
      await _context.SaveChangesAsync();

      var enterUrl = $"{Request.Scheme}://{Request.Host}{PaymentGatewayRedirect.BuildEnterGatewayPath(authority)}";
      var (html, contentType) = PaymentGatewayRedirect.BuildRedirectPage(enterUrl);
      return Content(html, contentType);
    }

    [AllowAnonymous]
    [HttpGet("/Payments/EnterGateway")]
    public async Task<IActionResult> EnterGateway(string authority)
    {
      if (string.IsNullOrWhiteSpace(authority))
        return BadRequest();

      var pendingCharge = await _context.ZarinpalChargeRequests
        .AnyAsync(z => z.Authority == authority && z.Status == "Pending");
      var pendingTicket = await _context.ZarinpalTicketPayments
        .AnyAsync(z => z.Authority == authority && z.Status == "Pending");

      if (!pendingCharge && !pendingTicket)
        return NotFound();

      var (html, contentType) = await PaymentGatewayRedirect.ResolveEnterGatewayAsync(_payment, authority);
      return Content(html, contentType);
    }

    [AllowAnonymous]
    [HttpGet("/Payments/ZarinpalCallback")]
    public async Task<IActionResult> ZarinpalCallback(string Authority, string Status)
    {
      if (Status != "OK")
      {
        TempData["PaymentError"] = "پرداخت لغو شد یا ناموفق بود";
        return RedirectToAction("Index", "Agency");
      }

      var zarinpalRequest = await _context.ZarinpalChargeRequests
          .Include(z => z.Agency)
          .FirstOrDefaultAsync(z => z.Authority == Authority);

      if (zarinpalRequest == null)
      {
        TempData["PaymentError"] = "درخواست پرداخت یافت نشد";
        return RedirectToAction("Index", "Agency");
      }

      // Idempotent — redirect if already processed
      if (zarinpalRequest.Status == "Success")
      {
        TempData["PaymentSuccess"] = $"پرداخت قبلاً تایید شده بود. کد پیگیری: {zarinpalRequest.RefId}";
        return RedirectToAction("Index", "Agency");
      }

      int amountRials = zarinpalRequest.AmountToman * 10;
      var (success, refId, cardPan, verifyMessage) = await _payment.VerifyPaymentAsync(Authority, amountRials);

      if (!success)
      {
        zarinpalRequest.Status = "Failed";
        await _context.SaveChangesAsync();
        TempData["PaymentError"] = verifyMessage;
        return RedirectToAction("Index", "Agency");
      }

      zarinpalRequest.Status = "Success";
      zarinpalRequest.PaidAt = DateTime.Now;
      zarinpalRequest.RefId = refId;
      zarinpalRequest.CardPan = cardPan;

      var agency = zarinpalRequest.Agency;

      // Save local balance first — payment is confirmed regardless of ORS availability
      var balanceCharge = new AgencyBalanceCharge
      {
        Amount = zarinpalRequest.AmountToman,
        ChargedAt = DateTime.Now,
        PaymentID = refId.ToString(),
        Description = $"شارژ آنلاین زرین‌پال - کارت: {cardPan}",
        Agency = agency
      };

      _context.Attach(agency);
      _context.AgencyBalanceCharges.Add(balanceCharge);
      await _context.SaveChangesAsync();

      // Charge ORS balance — non-blocking, admin can retry manually if ORS is down
      _apiClient.SetSellerApiKey(agency.ORSAPI_token);
      await _apiClient.ChargeOTABalanceAsync(zarinpalRequest.AmountToman);

      TempData["PaymentSuccess"] = $"حساب شما با موفقیت شارژ شد. کد پیگیری: {refId}";
      TempData["PaymentRefId"] = refId.ToString();
      return RedirectToAction("Index", "Agency");
    }

    [AllowAnonymous]
    [HttpGet("/Payments/ZarinpalTicketCallback")]
    public async Task<IActionResult> ZarinpalTicketCallback(string Authority, string Status)
    {
      if (Status != "OK")
      {
        TempData["ErrorMessage"] = "پرداخت لغو شد یا ناموفق بود";
        return RedirectToAction("Index", "TaxiTrips", new { area = "AgencyArea" });
      }

      var pending = await _context.ZarinpalTicketPayments
        .Include(z => z.Agency)
        .FirstOrDefaultAsync(z => z.Authority == Authority);

      if (pending == null)
      {
        TempData["ErrorMessage"] = "درخواست پرداخت بلیط یافت نشد";
        return RedirectToAction("Index", "TaxiTrips", new { area = "AgencyArea" });
      }

      if (pending.Status == "Success" && pending.TicketId is int existingId)
      {
        var existing = await _context.Tickets.FindAsync(existingId);
        if (existing != null)
          return RedirectToAction("ReserveConfirmed", "Reserve", new { area = "AgencyArea", ticketcode = existing.TicketCode });
      }

      int amountRials = pending.AmountToman * 10;
      var (success, refId, cardPan, verifyMessage) = await _payment.VerifyPaymentAsync(Authority, amountRials);
      if (!success)
      {
        pending.Status = "Failed";
        await _context.SaveChangesAsync();
        TempData["ErrorMessage"] = verifyMessage;
        return RedirectToAction("Reservetrip", "Reserve", new { area = "AgencyArea", tripcode = pending.TripCode });
      }

      pending.Status = "Success";
      pending.PaidAt = DateTime.Now;
      pending.RefId = refId;
      pending.CardPan = cardPan;

      var seller = pending.Agency;
      if (seller == null || string.IsNullOrWhiteSpace(seller.ORSAPI_token))
      {
        await _context.SaveChangesAsync();
        TempData["ErrorMessage"] = "پرداخت موفق بود اما اطلاعات آژانس برای صدور بلیط در دسترس نیست. با پشتیبانی تماس بگیرید.";
        return RedirectToAction("Reservetrip", "Reserve", new { area = "AgencyArea", tripcode = pending.TripCode });
      }

      _apiClient.SetSellerApiKey(seller.ORSAPI_token);

      // Gateway amount tops up ORS; hybrid relies on existing wallet + this charge covering full ticket.
      var charged = await _apiClient.ChargeOTABalanceAsync(pending.AmountToman);
      if (!charged)
      {
        _logger.LogError(
          "ORS ChargeOTA failed after ZarinPal verify. Authority={Authority} amount={Amount}",
          Authority, pending.AmountToman);
      }

      // Ensure ORS balance can cover the full ticket net (handles hybrid shortfall / failed charge).
      try
      {
        var balStr = await _apiClient.GetAccountBalance();
        var balance = (int)Convert.ToDouble(balStr ?? "0");
        var needed = pending.TicketPriceToman > 0 ? pending.TicketPriceToman : pending.AmountToman + pending.WalletAppliedToman;
        if (needed > 0 && balance < needed)
        {
          var topUp = needed - balance;
          _logger.LogWarning(
            "Topping up ORS shortfall after gateway. Authority={Authority} balance={Balance} needed={Needed} topUp={TopUp}",
            Authority, balance, needed, topUp);
          await _apiClient.ChargeOTABalanceAsync(topUp);
        }
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Could not verify/top-up ORS balance after ZarinPal. Authority={Authority}", Authority);
      }

      TicketConfirmationResponse reserveResponse;
      try
      {
        var tempreserve = new TicketTempReserveRequestModel { isPrivate = true, tripCode = pending.TripCode };
        var reservecode = await _apiClient.ReserveTicketTemporarirly(tempreserve);
        var confirm = new ConfirmReserveRequestModel
        {
          passengerFirstName = pending.Firstname,
          passengerLastName = pending.Lastname,
          reservationCode = reservecode,
          passengerNationalCode = pending.Nacode,
          passengerNumberPhone = pending.Numberphone,
          passengerCompanyName = pending.CompanyName ?? ""
        };

        reserveResponse = await _apiClient.ConfirmReserve(confirm);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "ORS ticket issue failed after ZarinPal ticket payment. Authority={Authority}", Authority);
        await _context.SaveChangesAsync();
        TempData["ErrorMessage"] = "پرداخت موفق بود اما صدور بلیط با خطا مواجه شد. با پشتیبانی تماس بگیرید. " +
                                   (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"({ex.Message})");
        // Stay in reserve flow — not Agency dashboard
        return RedirectToAction("Reservetrip", "Reserve", new { area = "AgencyArea", tripcode = pending.TripCode });
      }

      var trip = await _apiClient.GetTripInfo(pending.TripCode);
      var ticket = new Ticket
      {
        Firstname = pending.Firstname,
        Lastname = pending.Lastname,
        PhoneNumber = pending.Numberphone,
        NaCode = pending.Nacode,
        CompanyName = pending.CompanyName,
        HeadOfPassengers = pending.HeadOfPassengers,
        TicketFinalPrice = reserveResponse.paid_total_fee_tomans,
        Gender = pending.Gender,
        TicketOriginalPrice = trip.afterdiscticketprice,
        TripOrigin = trip.originCityName,
        TripDestination = trip.destinationCityName,
        RegisteredAt = DateTime.Now,
        TicketCode = reserveResponse.ticketCode,
        Tripcode = trip.tripPlanCode,
        ServiceName = trip.taxiSupervisorName,
        CarName = trip.carModelName,
        Agency = seller,
        AgencyEmployeeId = pending.AgencyEmployeeId
          ?? await _context.AgencyEmployees
              .Where(e => e.AgencyId == seller.Id && e.NaCode == pending.Nacode)
              .Select(e => (int?)e.Id)
              .FirstOrDefaultAsync()
          ?? await _context.AgencyEmployees
              .Where(e => e.AgencyId == seller.Id && e.PhoneNumber == pending.Numberphone)
              .Select(e => (int?)e.Id)
              .FirstOrDefaultAsync()
      };

      _context.Attach(seller);
      _context.Tickets.Add(ticket);
      await _context.SaveChangesAsync();
      pending.TicketId = ticket.Id;
      await AttachPendingCompanionsAsync(ticket.Id, pending.CompanionsJson, seller.Id);
      await _context.SaveChangesAsync();

      try
      {
        await _sms.SendCustomerTicket_issued(ticket.Firstname, ticket.Lastname, ticket.TicketCode, ticket.TicketCode, ticket.PhoneNumber);
      }
      catch
      {
      }

      return RedirectToAction("ReserveConfirmed", "Reserve", new { area = "AgencyArea", ticketcode = ticket.TicketCode });
    }

    private async Task AttachPendingCompanionsAsync(int ticketId, string? companionsJson, int agencyId)
    {
      if (string.IsNullOrWhiteSpace(companionsJson)) return;

      List<Application.ViewModels.Reserve.CompanionPassengerViewModel>? parsed;
      try
      {
        parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Application.ViewModels.Reserve.CompanionPassengerViewModel>>(companionsJson);
      }
      catch
      {
        return;
      }

      if (parsed == null || parsed.Count == 0) return;

      var order = 1;
      foreach (var c in parsed.Take(2))
      {
        if (string.IsNullOrWhiteSpace(c.Firstname) || string.IsNullOrWhiteSpace(c.Lastname) ||
            string.IsNullOrWhiteSpace(c.Gender) || string.IsNullOrWhiteSpace(c.NaCode))
          continue;

        int? employeeId = null;
        if (c.EmployeeId is int eid && eid > 0)
        {
          var ok = await _context.AgencyEmployees.AnyAsync(e => e.Id == eid && e.AgencyId == agencyId);
          if (ok) employeeId = eid;
        }
        if (employeeId == null)
        {
          var na = c.NaCode.Trim();
          employeeId = await _context.AgencyEmployees
            .Where(e => e.AgencyId == agencyId && e.NaCode == na)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();
        }

        _context.TicketCompanions.Add(new TicketCompanion
        {
          TicketId = ticketId,
          SortOrder = order++,
          Firstname = c.Firstname.Trim(),
          Lastname = c.Lastname.Trim(),
          Gender = c.Gender.Trim(),
          NaCode = c.NaCode.Trim(),
          PhoneNumber = string.IsNullOrWhiteSpace(c.PhoneNumber) ? null : c.PhoneNumber.Trim(),
          AgencyEmployeeId = employeeId
        });
      }
    }
  }
}
