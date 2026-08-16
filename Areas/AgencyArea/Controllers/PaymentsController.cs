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

      return Redirect(_payment.GetPaymentGatewayUrl(authority));
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
        return RedirectToAction("Index", "Home", new { area = "AgencyArea" });
      }

      var pending = await _context.ZarinpalTicketPayments
        .Include(z => z.Agency)
        .FirstOrDefaultAsync(z => z.Authority == Authority);

      if (pending == null)
      {
        TempData["ErrorMessage"] = "درخواست پرداخت بلیط یافت نشد";
        return RedirectToAction("Index", "Home", new { area = "AgencyArea" });
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
      _apiClient.SetSellerApiKey(seller.ORSAPI_token);
      await _apiClient.ChargeOTABalanceAsync(pending.AmountToman);

      var tempreserve = new TicketTempReserveRequestModel { isPrivate = true, tripCode = pending.TripCode };
      var reservecode = await _apiClient.ReserveTicketTemporarirly(tempreserve);
      var confirm = new ConfirmReserveRequestModel
      {
        passengerFirstName = pending.Firstname,
        passengerLastName = pending.Lastname,
        reservationCode = reservecode,
        passengerNationalCode = pending.Nacode,
        passengerNumberPhone = pending.Numberphone
      };

      TicketConfirmationResponse reserveResponse;
      try
      {
        reserveResponse = await _apiClient.ConfirmReserve(confirm);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "ORS confirm failed after ZarinPal ticket payment. Authority={Authority}", Authority);
        await _context.SaveChangesAsync();
        TempData["ErrorMessage"] = "پرداخت موفق بود اما صدور بلیط با خطا مواجه شد. با پشتیبانی تماس بگیرید.";
        return RedirectToAction("Index", "Agency");
      }

      var trip = await _apiClient.GetTripInfo(pending.TripCode);
      var ticket = new Ticket
      {
        Firstname = pending.Firstname,
        Lastname = pending.Lastname,
        PhoneNumber = pending.Numberphone,
        NaCode = pending.Nacode,
        TicketFinalPrice = reserveResponse.paid_total_fee_tomans,
        Gender = pending.Gender,
        TicketOriginalPrice = trip.originalTicketprice,
        TripOrigin = trip.originCityName,
        TripDestination = trip.destinationCityName,
        RegisteredAt = DateTime.Now,
        TicketCode = reserveResponse.ticketCode,
        Tripcode = trip.tripPlanCode,
        ServiceName = trip.taxiSupervisorName,
        CarName = trip.carModelName,
        Agency = seller
      };

      _context.Attach(seller);
      _context.Tickets.Add(ticket);
      await _context.SaveChangesAsync();
      pending.TicketId = ticket.Id;
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
  }
}
