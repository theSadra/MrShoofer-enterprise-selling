using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.Areas.AgencyArea;
using Application.Areas.AgencyArea.Controllers;
using Application.Data;
using Application.Models;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.Services.Payment;
using Application.ViewModels.Reserve;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Application.Tests;

public class PaymentAndReservationFlowTests : IAsyncLifetime
{
  private const int TicketPrice = 50_000;
  private const int ChargeToman = 80_000;
  private const string TripCode = "TRIP-FUNC-001";
  private const string AgencyUserName = "09120000000";

  private SqliteConnection _connection = null!;
  private AppDbContext _db = null!;
  private UserManager<IdentityUser> _userManager = null!;
  private IdentityUser _agencyUser = null!;
  private Agency _agency = null!;
  private FakePaymentService _payments = null!;
  private OrsFakeHandler _ors = null!;
  private MrShooferAPIClient _api = null!;

  public async Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    await _connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseSqlite(_connection)
      .Options;

    _db = new AppDbContext(options);
    await _db.Database.EnsureCreatedAsync();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(options);
    services.AddScoped(_ => new AppDbContext(options));
    services.AddIdentityCore<IdentityUser>()
      .AddEntityFrameworkStores<AppDbContext>()
      .AddDefaultTokenProviders();
    services.AddScoped<IUserStore<IdentityUser>, UserStore<IdentityUser, IdentityRole, AppDbContext>>();

    var sp = services.BuildServiceProvider();
    _userManager = new UserManager<IdentityUser>(
      new UserStore<IdentityUser, IdentityRole, AppDbContext>(_db),
      Options.Create(new IdentityOptions()),
      new PasswordHasher<IdentityUser>(),
      Array.Empty<IUserValidator<IdentityUser>>(),
      Array.Empty<IPasswordValidator<IdentityUser>>(),
      new UpperInvariantLookupNormalizer(),
      new IdentityErrorDescriber(),
      sp,
      NullLogger<UserManager<IdentityUser>>.Instance);

    _agencyUser = new IdentityUser { UserName = AgencyUserName, PhoneNumber = AgencyUserName };
    var created = await _userManager.CreateAsync(_agencyUser, "Pass1234");
    Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
    _agencyUser = (await _userManager.FindByNameAsync(AgencyUserName))!;

    _agency = new Agency
    {
      Name = "Test Agency",
      Address = "Tehran",
      AdminMobile = AgencyUserName,
      DateJoined = DateTime.Now,
      ORSAPI_token = "agency-ors-token",
      Commission = 10,
      IdentityUser = _agencyUser
    };
    _db.Agencies.Add(_agency);
    await _db.SaveChangesAsync();
    _db.ChangeTracker.Clear();

    _payments = new FakePaymentService();
    _ors = new OrsFakeHandler(TripCode, TicketPrice);
    _api = new MrShooferAPIClient(new HttpClient(_ors) { BaseAddress = new Uri("http://ors.test") });
  }

  public async Task DisposeAsync()
  {
    await _db.DisposeAsync();
    await _connection.DisposeAsync();
  }

  [Fact]
  public async Task Zarinpal_sandbox_payment_request_returns_authority()
  {
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Zarinpal:MerchantId"] = "a3348b1d-3593-4aa0-922c-f539bf8f9ae3",
      ["Zarinpal:IsSandbox"] = "true",
      ["Zarinpal:PaymentUrl"] = "https://sandbox.zarinpal.com/pg/v4/payment/request.json",
      ["Zarinpal:VerifyUrl"] = "https://sandbox.zarinpal.com/pg/v4/payment/verify.json",
      ["Zarinpal:PaymentGatewayUrl"] = "https://sandbox.zarinpal.com/pg/StartPay/",
      ["Zarinpal:CallbackUrl"] = "http://localhost:5000/Payments/ZarinpalCallback"
    }).Build();

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    var zarinpal = new ZarinpalService(http, config);

    var (success, authority, message) = await zarinpal.RequestPaymentAsync(
      amountRials: 10_000,
      description: "functional test charge",
      mobile: "09120000000");

    Assert.True(success, $"Sandbox request failed: {message}");
    Assert.False(string.IsNullOrWhiteSpace(authority));
    Assert.StartsWith("https://sandbox.zarinpal.com/pg/StartPay/", zarinpal.GetPaymentGatewayUrl(authority));

    var (verifyOk, _, _, verifyMsg) = await zarinpal.VerifyPaymentAsync(authority, 10_000);
    Assert.False(verifyOk);
    Assert.False(string.IsNullOrWhiteSpace(verifyMsg));
  }

  [Fact]
  public async Task Charge_then_callback_then_reserve_issues_ticket()
  {
    var payments = CreatePaymentsController(authenticated: true);

    var tooSmall = await payments.InitiateZarinpalCharge(500);
    Assert.IsType<BadRequestObjectResult>(tooSmall);

    var start = await payments.InitiateZarinpalCharge(ChargeToman);
    var redirect = Assert.IsType<RedirectResult>(start);
    Assert.Equal($"https://sandbox.zarinpal.com/pg/StartPay/{_payments.LastAuthority}", redirect.Url);

    var pending = await _db.ZarinpalChargeRequests.SingleAsync();
    Assert.Equal("Pending", pending.Status);
    Assert.Equal(ChargeToman, pending.AmountToman);
    Assert.Equal(_payments.LastAuthority, pending.Authority);

    var cancelled = await CreatePaymentsController(authenticated: false)
      .ZarinpalCallback(_payments.LastAuthority, "NOK");
    Assert.IsType<RedirectToActionResult>(cancelled);
    Assert.Equal("Pending", (await _db.ZarinpalChargeRequests.SingleAsync()).Status);
    Assert.Empty(_db.AgencyBalanceCharges);

    var unknown = await CreatePaymentsController(authenticated: false)
      .ZarinpalCallback("A000000000000000000000000000MISSING", "OK");
    Assert.IsType<RedirectToActionResult>(unknown);

    _ors.BalanceTomans = 0;
    var callback = await CreatePaymentsController(authenticated: false)
      .ZarinpalCallback(_payments.LastAuthority, "OK");
    var callbackRedirect = Assert.IsType<RedirectToActionResult>(callback);
    Assert.Equal("Index", callbackRedirect.ActionName);

    await _db.Entry(pending).ReloadAsync();
    Assert.Equal("Success", pending.Status);
    Assert.Equal(_payments.RefId, pending.RefId);
    Assert.Equal("1234****5678", pending.CardPan);

    var localCharge = await _db.AgencyBalanceCharges.SingleAsync();
    Assert.Equal(ChargeToman, localCharge.Amount);
    Assert.Equal(_payments.RefId.ToString(), localCharge.PaymentID);
    Assert.Equal(ChargeToman, _ors.BalanceTomans);
    Assert.Equal("agency-ors-token", _ors.LastBearerToken);
    Assert.Equal(ChargeToman * 10, _payments.VerifiedAmountRials);

    var replay = await CreatePaymentsController(authenticated: false)
      .ZarinpalCallback(_payments.LastAuthority, "OK");
    Assert.IsType<RedirectToActionResult>(replay);
    Assert.Equal(1, await _db.AgencyBalanceCharges.CountAsync());
    Assert.Equal(1, _ors.ChargeCallCount);

    var reserve = CreateReserveController(authenticated: true);

    var guest = CreateReserveController(authenticated: false);
    var guestPost = await guest.Reservetrip(ValidReserveForm());
    var guestJson = Assert.IsType<JsonResult>(guestPost);
    var guestPayload = JsonSerializer.SerializeToElement(guestJson.Value);
    Assert.True(guestPayload.GetProperty("requiresAuth").GetBoolean());

    _ors.BalanceTomans = TicketPrice - 1;
    var poor = await reserve.Reservetrip(ValidReserveForm());
    var poorView = Assert.IsType<ViewResult>(poor);
    Assert.Equal("ConfirmInfo", poorView.ViewName);
    Assert.False((bool)reserve.ViewBag.canbuy);

    _ors.BalanceTomans = ChargeToman;
    var confirmPage = await reserve.Reservetrip(ValidReserveForm());
    var confirmView = Assert.IsType<ViewResult>(confirmPage);
    Assert.Equal("ConfirmInfo", confirmView.ViewName);

    var issued = await reserve.ConfirmInfo(ValidConfirmForm("balance"));
    var issuedRedirect = Assert.IsType<RedirectToActionResult>(issued);
    Assert.Equal("ReserveConfirmed", issuedRedirect.ActionName);
    Assert.Equal("TKT-FUNC-001", issuedRedirect.RouteValues?["ticketcode"]);

    var ticket = await _db.Tickets.Include(t => t.Agency).SingleAsync();
    Assert.Equal("TKT-FUNC-001", ticket.TicketCode);
    Assert.Equal(TripCode, ticket.Tripcode);
    Assert.Equal("Ali", ticket.Firstname);
    Assert.Equal("Rezaei", ticket.Lastname);
    Assert.Equal("09123456789", ticket.PhoneNumber);
    Assert.Equal("0012345678", ticket.NaCode);
    Assert.Equal(TicketPrice, ticket.TicketFinalPrice);
    Assert.Equal(_agency.Id, ticket.Agency.Id);
    Assert.Equal("TEMP-FUNC-001", _ors.LastReservationCode);
    Assert.Equal("Ali", _ors.LastPassengerFirstName);
  }

  [Fact]
  public async Task Zarinpal_ticket_pay_then_callback_issues_ticket()
  {
    var reserve = CreateReserveController(authenticated: true);
    _ors.BalanceTomans = 0;

    var confirmPage = await reserve.Reservetrip(ValidReserveForm());
    Assert.Equal("ConfirmInfo", Assert.IsType<ViewResult>(confirmPage).ViewName);

    var start = await reserve.ConfirmInfo(ValidConfirmForm("zarinpal"));
    var redirect = Assert.IsType<RedirectResult>(start);
    Assert.StartsWith("https://sandbox.zarinpal.com/pg/StartPay/", redirect.Url);

    var pending = await _db.ZarinpalTicketPayments.SingleAsync();
    Assert.Equal("Pending", pending.Status);
    Assert.Equal(TicketPrice, pending.AmountToman);
    Assert.Equal(TripCode, pending.TripCode);

    var callback = await CreatePaymentsController(authenticated: false)
      .ZarinpalTicketCallback(_payments.LastAuthority, "OK");
    var issued = Assert.IsType<RedirectToActionResult>(callback);
    Assert.Equal("ReserveConfirmed", issued.ActionName);
    Assert.Equal("TKT-FUNC-001", issued.RouteValues?["ticketcode"]);

    await _db.Entry(pending).ReloadAsync();
    Assert.Equal("Success", pending.Status);
    Assert.Equal(1, await _db.Tickets.CountAsync());
    Assert.Equal(1, _ors.ChargeCallCount);
  }

  [Fact]
  public async Task Callback_marks_failed_when_verify_rejects()
  {
    var payments = CreatePaymentsController(authenticated: true);
    await payments.InitiateZarinpalCharge(ChargeToman);
    _payments.VerifyShouldSucceed = false;

    var result = await CreatePaymentsController(authenticated: false)
      .ZarinpalCallback(_payments.LastAuthority, "OK");
    Assert.IsType<RedirectToActionResult>(result);

    var row = await _db.ZarinpalChargeRequests.SingleAsync();
    Assert.Equal("Failed", row.Status);
    Assert.Empty(_db.AgencyBalanceCharges);
    Assert.Equal(0, _ors.ChargeCallCount);
  }

  private AppDbContext NewDb() =>
    new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

  private PaymentsController CreatePaymentsController(bool authenticated)
  {
    var controller = new PaymentsController(
      NewDb(), _userManager, _payments, _api, NullLogger<PaymentsController>.Instance,
      new CustomerServiceSmsSender(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["smsirapikey"] = "test-key"
      }).Build()));
    BindController(controller, authenticated);
    return controller;
  }

  private ReserveController CreateReserveController(bool authenticated)
  {
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["smsirapikey"] = "test-key",
      ["MrShoofer:SellerToken"] = "guest-token",
      ["serivce_url"] = "http://localhost:5001",
      ["Zarinpal:TicketCallbackUrl"] = "http://localhost:5055/Payments/ZarinpalTicketCallback"
    }).Build();

    var controller = new ReserveController(
      _api, _userManager, NewDb(), new CustomerServiceSmsSender(config), config, _payments);
    BindController(controller, authenticated);
    controller.OnActionExecuting(new ActionExecutingContext(
      controller.ControllerContext,
      new List<IFilterMetadata>(),
      new Dictionary<string, object?>(),
      controller));
    return controller;
  }

  private void BindController(Controller controller, bool authenticated)
  {
    var http = new DefaultHttpContext();
    if (authenticated)
    {
      http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
      {
        new Claim(ClaimTypes.Name, AgencyUserName),
        new Claim(ClaimTypes.NameIdentifier, _agencyUser.Id),
        new Claim("Role", "Agency")
      }, IdentityConstants.ApplicationScheme));
    }

    controller.ControllerContext = new ControllerContext
    {
      HttpContext = http,
      RouteData = new RouteData(),
      ActionDescriptor = new ControllerActionDescriptor
      {
        ControllerName = controller.GetType().Name.Replace("Controller", ""),
        ActionName = "Index",
        ControllerTypeInfo = controller.GetType().GetTypeInfo()
      }
    };
    controller.TempData = new TempDataDictionary(http, new TestTempDataProvider());
    controller.Url = new StubUrlHelper(controller.ControllerContext);
  }

  private static ReserveInfoViewModel ValidReserveForm() => new()
  {
    Firstname = "Ali",
    Lastname = "Rezaei",
    Gender = "male",
    NaCode = "0012345678",
    NumebrPhone = "09123456789",
    TripCode = TripCode
  };

  private static ConfirmInfoViewModel ValidConfirmForm(string paymentMethod = "balance") => new()
  {
    Firstname = "Ali",
    Lastname = "Rezaei",
    Gender = "male",
    Nacode = "0012345678",
    Numberphone = "09123456789",
    TripCode = TripCode,
    PaymentMethod = paymentMethod
  };

  private sealed class FakePaymentService : IPaymentService
  {
    public string LastAuthority { get; private set; } = "";
    public long RefId { get; } = 99887766;
    public int VerifiedAmountRials { get; private set; }
    public bool VerifyShouldSucceed { get; set; } = true;

    public Task<(bool Success, string Authority, string Message)> RequestPaymentAsync(
      int amountRials, string description, string mobile, string? email = null, string? callbackUrl = null)
    {
      LastAuthority = "A" + Guid.NewGuid().ToString("N");
      return Task.FromResult((true, LastAuthority, "موفق"));
    }

    public Task<(bool Success, long RefId, string CardPan, string Message)> VerifyPaymentAsync(
      string authority, int amountRials)
    {
      VerifiedAmountRials = amountRials;
      if (!VerifyShouldSucceed)
        return Task.FromResult((false, 0L, "", "verify rejected"));
      return Task.FromResult((true, RefId, "1234****5678", "پرداخت تایید شد"));
    }

    public string GetPaymentGatewayUrl(string authority) =>
      $"https://sandbox.zarinpal.com/pg/StartPay/{authority}";
  }

  private sealed class OrsFakeHandler : HttpMessageHandler
  {
    private readonly string _tripCode;
    private readonly int _ticketPrice;

    public int BalanceTomans { get; set; }
    public int ChargeCallCount { get; private set; }
    public string? LastBearerToken { get; private set; }
    public string? LastReservationCode { get; private set; }
    public string? LastPassengerFirstName { get; private set; }

    public OrsFakeHandler(string tripCode, int ticketPrice)
    {
      _tripCode = tripCode;
      _ticketPrice = ticketPrice;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request, CancellationToken cancellationToken)
    {
      LastBearerToken = request.Headers.Authorization?.Parameter;
      var path = request.RequestUri!.AbsolutePath;

      if (path.Contains("/Account/getAccountBalance", StringComparison.OrdinalIgnoreCase))
        return Json(new { accountBalance_tomans = BalanceTomans });

      if (path.Contains("/Trips/getTripinfo", StringComparison.OrdinalIgnoreCase))
      {
        return Json(new
        {
          tripPlanCode = _tripCode,
          originalTicketprice = _ticketPrice,
          afterdiscticketprice = _ticketPrice,
          originCityName = "تهران",
          destinationCityName = "اصفهان",
          taxiSupervisorName = "تست سرویس",
          carModelName = "پژو"
        });
      }

      if (path.Contains("/Tickets/reserverTemporarily", StringComparison.OrdinalIgnoreCase))
        return Json(new { ticketCode = "TEMP-FUNC-001" });

      if (path.Contains("/Tickets/confirmReserve", StringComparison.OrdinalIgnoreCase))
      {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        LastReservationCode = doc.RootElement.GetProperty("reservationCode").GetString();
        LastPassengerFirstName = doc.RootElement.GetProperty("passengerFirstName").GetString();
        return Json(new
        {
          message = "ok",
          ticketCode = "TKT-FUNC-001",
          paid_total_fee_tomans = _ticketPrice,
          remainAccountBalance = BalanceTomans - _ticketPrice,
          seatnumber = "1",
          isprivateTrip = true
        });
      }

      if (path.Contains("/OTAManagement/ChargeOTA", StringComparison.OrdinalIgnoreCase))
      {
        var form = await request.Content!.ReadAsStringAsync(cancellationToken);
        var amount = int.Parse(form.Split('=')[1]);
        BalanceTomans += amount;
        ChargeCallCount++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
      }

      return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object payload) =>
      new(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
      };
  }

  private sealed class TestTempDataProvider : ITempDataProvider
  {
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
  }

  private sealed class StubUrlHelper : IUrlHelper
  {
    public StubUrlHelper(ActionContext actionContext) => ActionContext = actionContext;
    public ActionContext ActionContext { get; }
    public string? Action(UrlActionContext actionContext) => "/Reserve/Reservetrip";
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => true;
    public string? Link(string? routeName, object? values) => "/";
    public string? RouteUrl(UrlRouteContext routeContext) => "/";
  }
}
