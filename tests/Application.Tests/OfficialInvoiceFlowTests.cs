using System.Security.Claims;
using Application.Data;
using Application.Models;
using Application.Services.OfficialInvoices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using AdminOfficialInvoicesController = Application.Areas.Admin.Controllers.OfficialInvoicesController;
using AgencyOfficialInvoicesController = Application.Areas.AgencyArea.Controllers.OfficialInvoicesController;

namespace Application.Tests;

public class OfficialInvoiceFlowTests : IAsyncLifetime
{
  private SqliteConnection _connection = null!;
  private AppDbContext _db = null!;
  private UserManager<IdentityUser> _userManager = null!;
  private IdentityUser _agencyUser = null!;
  private Agency _agency = null!;
  private FakeWebHostEnvironment _env = null!;

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

    _agencyUser = new IdentityUser { UserName = "09121110000", PhoneNumber = "09121110000" };
    Assert.True((await _userManager.CreateAsync(_agencyUser, "Pass1234")).Succeeded);
    _agencyUser = (await _userManager.FindByNameAsync("09121110000"))!;

    _agency = new Agency
    {
      Name = "Invoice Agency",
      Address = "Tehran",
      AdminMobile = "09121110000",
      DateJoined = DateTime.Now,
      ORSAPI_token = "token",
      Commission = 10,
      PanelType = AgencyPanelType.Organization,
      IdentityUser = _agencyUser
    };
    _db.Agencies.Add(_agency);
    _db.InvoiceSerialConfigs.Add(new InvoiceSerialConfig { Pattern = "{n}-{prefix}", Prefix = "2209", NextSequence = 1 });
    await _db.SaveChangesAsync();
    _db.ChangeTracker.Clear();

    _env = new FakeWebHostEnvironment();
  }

  public async Task DisposeAsync()
  {
    await _db.DisposeAsync();
    await _connection.DisposeAsync();
  }

  [Fact]
  public async Task Agency_creates_invoice_from_uncancelled_ticket_with_autofill_and_pending_status()
  {
    await SeedFinancialProfileAsync();
    var ticket = await SeedTicketAsync(cancelled: false, priceTomans: 75_000);
    var agencyCtrl = CreateAgencyController();

    var get = await agencyCtrl.Create(ticket.TicketCode);
    var getView = Assert.IsType<ViewResult>(get);
    var vm = Assert.IsType<Application.Areas.AgencyArea.ViewModels.OfficialInvoices.OfficialInvoiceCreateViewModel>(getView.Model);
    Assert.Equal(ticket.TicketCode, vm.TicketCode);
    Assert.Equal("Invoice Agency", vm.BuyerName);
    Assert.Equal(75_000, vm.Lines.Single().UnitAmountTomans);
    Assert.Equal(OfficialInvoiceSeller.MoodianNote, vm.Notes);

    var post = await agencyCtrl.CreateConfirm(ticket.TicketCode);
    var redirect = Assert.IsType<RedirectToActionResult>(post);
    Assert.Equal(nameof(AgencyOfficialInvoicesController.Print), redirect.ActionName);

    var invoice = await NewDb().OfficialInvoices.SingleAsync();
    Assert.Equal(ticket.Id, invoice.TicketId);
    Assert.Equal(ticket.TicketCode, invoice.TicketCode);
    Assert.Equal("Invoice Agency", invoice.BuyerName);
    Assert.Equal(750_000, invoice.PayableRials);
    Assert.Equal(OfficialInvoiceUploadStatus.Pending, invoice.UploadStatus);
    Assert.Equal(OfficialInvoiceUploadStatusText.Pending, invoice.UploadStatusText);
    Assert.Equal(OfficialInvoiceSeller.MoodianNote, invoice.Notes);
    Assert.Equal("1-2209", invoice.SerialNumber);
    Assert.Contains("۵ روز کاری", OfficialInvoiceSeller.MoodianNote);
    Assert.Contains("سامانه مودیان", OfficialInvoiceSeller.MoodianNote);
  }

  [Fact]
  public async Task Agency_cannot_create_invoice_without_financial_profile()
  {
    var ticket = await SeedTicketAsync(cancelled: false, priceTomans: 40_000);
    var agencyCtrl = CreateAgencyController();

    var get = await agencyCtrl.Create(ticket.TicketCode);
    var getRedirect = Assert.IsType<RedirectToActionResult>(get);
    Assert.Equal("LegalProfile", getRedirect.ActionName);
    Assert.Equal("Agency", getRedirect.ControllerName);

    var post = await agencyCtrl.CreateConfirm(ticket.TicketCode);
    var postRedirect = Assert.IsType<RedirectToActionResult>(post);
    Assert.Equal("LegalProfile", postRedirect.ActionName);
    Assert.Equal(0, await NewDb().OfficialInvoices.CountAsync());
  }

  [Fact]
  public async Task Agency_invoice_buyer_uses_saved_legal_financial_profile()
  {
    var db = NewDb();
    var agency = await db.Agencies.SingleAsync(a => a.Id == _agency.Id);
    agency.EconomicNo = "12345678901";
    agency.RegistrationNo = "998877";
    agency.NationalId = "14000000001";
    agency.PhoneNumber = "02188990011";
    agency.Fax = "02188990012";
    agency.Province = "تهران";
    agency.County = "تهران";
    agency.City = "تهران";
    agency.PostalCode = "1234567890";
    agency.Address = "خیابان نمونه";
    await db.SaveChangesAsync();

    var ticket = await SeedTicketAsync(cancelled: false, priceTomans: 50_000);
    var agencyCtrl = CreateAgencyController();

    var get = await agencyCtrl.Create(ticket.TicketCode);
    var getView = Assert.IsType<ViewResult>(get);
    var vm = Assert.IsType<Application.Areas.AgencyArea.ViewModels.OfficialInvoices.OfficialInvoiceCreateViewModel>(getView.Model);
    Assert.Equal("12345678901", vm.BuyerEconomicNo);
    Assert.Equal("998877", vm.BuyerRegistrationNo);
    Assert.Equal("14000000001", vm.BuyerNationalId);
    Assert.Equal("02188990011", vm.BuyerPhone);
    Assert.Equal("02188990012", vm.BuyerFax);
    Assert.Equal("تهران", vm.BuyerProvince);
    Assert.Equal("تهران", vm.BuyerCounty);
    Assert.Equal("تهران", vm.BuyerCity);
    Assert.Equal("1234567890", vm.BuyerPostalCode);
    Assert.Equal("خیابان نمونه", vm.BuyerAddress);

    await agencyCtrl.CreateConfirm(ticket.TicketCode);
    var invoice = await NewDb().OfficialInvoices.SingleAsync();
    Assert.Equal("12345678901", invoice.BuyerEconomicNo);
    Assert.Equal("998877", invoice.BuyerRegistrationNo);
    Assert.Equal("14000000001", invoice.BuyerNationalId);
    Assert.Equal("02188990011", invoice.BuyerPhone);
    Assert.Equal("02188990012", invoice.BuyerFax);
    Assert.Equal("تهران", invoice.BuyerProvince);
    Assert.Equal("خیابان نمونه", invoice.BuyerAddress);
  }

  [Fact]
  public async Task Agency_cannot_create_invoice_for_cancelled_ticket()
  {
    await SeedFinancialProfileAsync();
    var ticket = await SeedTicketAsync(cancelled: true, priceTomans: 40_000);
    var agencyCtrl = CreateAgencyController();

    var get = await agencyCtrl.Create(ticket.TicketCode);
    Assert.Equal(nameof(AgencyOfficialInvoicesController.Index), Assert.IsType<RedirectToActionResult>(get).ActionName);
    Assert.Equal(0, await NewDb().OfficialInvoices.CountAsync());

    var post = await agencyCtrl.CreateConfirm(ticket.TicketCode);
    Assert.Equal(nameof(AgencyOfficialInvoicesController.Index), Assert.IsType<RedirectToActionResult>(post).ActionName);
    Assert.Equal(0, await NewDb().OfficialInvoices.CountAsync());
  }

  [Fact]
  public async Task Admin_can_mark_uploaded_and_agency_has_no_status_change_action()
  {
    await SeedFinancialProfileAsync();
    var ticket = await SeedTicketAsync(cancelled: false, priceTomans: 10_000);
    await CreateAgencyController().CreateConfirm(ticket.TicketCode);
    var invoice = await NewDb().OfficialInvoices.SingleAsync();
    Assert.Equal(OfficialInvoiceUploadStatus.Pending, invoice.UploadStatus);

    var admin = CreateAdminController();
    var set = await admin.SetStatus(invoice.Id, (int)OfficialInvoiceUploadStatus.Uploaded);
    Assert.Equal(nameof(AdminOfficialInvoicesController.Index),
      Assert.IsType<RedirectToActionResult>(set).ActionName);

    invoice = await NewDb().OfficialInvoices.SingleAsync();
    Assert.Equal(OfficialInvoiceUploadStatus.Uploaded, invoice.UploadStatus);
    Assert.Equal(OfficialInvoiceUploadStatusText.Uploaded, invoice.UploadStatusText);
    Assert.Equal("admin-user", invoice.UploadedBy);
    Assert.NotNull(invoice.UploadedAt);

    var agencyMethods = typeof(AgencyOfficialInvoicesController)
      .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)
      .Select(m => m.Name)
      .ToHashSet(StringComparer.Ordinal);
    Assert.DoesNotContain("SetStatus", agencyMethods);
    Assert.DoesNotContain("SetUploadStatus", agencyMethods);
  }

  [Fact]
  public async Task Service_rejects_blank_invoice_without_ticket_data()
  {
    var service = new OfficialInvoiceService(NewDb(), _env);
    await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new OfficialInvoice
    {
      AgencyId = _agency.Id,
      BuyerName = "X",
      Lines = new List<OfficialInvoiceLine>()
    }));
  }

  private async Task SeedFinancialProfileAsync()
  {
    var db = NewDb();
    var agency = await db.Agencies.SingleAsync(a => a.Id == _agency.Id);
    agency.EconomicNo = "12345678901";
    agency.RegistrationNo = "998877";
    agency.NationalId = "14000000001";
    await db.SaveChangesAsync();
  }

  private async Task<Ticket> SeedTicketAsync(bool cancelled, int priceTomans)
  {
    var db = NewDb();
    var agency = await db.Agencies.Include(a => a.IdentityUser).SingleAsync(a => a.Id == _agency.Id);
    var ticket = new Ticket
    {
      TicketCode = cancelled ? "TKT-CANCEL" : "TKT-OK-001",
      Tripcode = "TRIP-1",
      Firstname = "Ali",
      Lastname = "Testi",
      Gender = "male",
      PhoneNumber = "09120001122",
      NaCode = "0011223344",
      CompanyName = "",
      DOB = new DateTime(1990, 1, 1),
      TicketOriginalPrice = priceTomans,
      TicketFinalPrice = priceTomans,
      TripOrigin = "تهران",
      TripDestination = "اصفهان",
      RegisteredAt = DateTime.Now,
      IsCancelled = cancelled,
      ServiceName = "سواری",
      CarName = "پژو",
      Agency = agency
    };
    db.Tickets.Add(ticket);
    await db.SaveChangesAsync();
    return ticket;
  }

  private AgencyOfficialInvoicesController CreateAgencyController()
  {
    var db = NewDb();
    var controller = new AgencyOfficialInvoicesController(db, new OfficialInvoiceService(db, _env));
    BindController(controller, agencyUser: true);
    return controller;
  }

  private AdminOfficialInvoicesController CreateAdminController()
  {
    var db = NewDb();
    var controller = new AdminOfficialInvoicesController(db, new OfficialInvoiceService(db, _env));
    BindController(controller, agencyUser: false);
    return controller;
  }

  private AppDbContext NewDb() =>
    new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

  private void BindController(Controller controller, bool agencyUser)
  {
    var http = new DefaultHttpContext();
    if (agencyUser)
    {
      http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
      {
        new Claim(ClaimTypes.Name, _agencyUser.UserName!),
        new Claim(ClaimTypes.NameIdentifier, _agencyUser.Id),
        new Claim("Role", "Agency")
      }, IdentityConstants.ApplicationScheme));
    }
    else
    {
      http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
      {
        new Claim(ClaimTypes.Name, "admin-user"),
        new Claim(ClaimTypes.NameIdentifier, "admin-id"),
        new Claim("Role", "Admin")
      }, IdentityConstants.ApplicationScheme));
    }

    controller.ControllerContext = new ControllerContext
    {
      HttpContext = http,
      RouteData = new RouteData(),
      ActionDescriptor = new ControllerActionDescriptor
      {
        ControllerName = controller.GetType().Name.Replace("Controller", "", StringComparison.Ordinal),
        ActionName = "Index"
      }
    };
    controller.TempData = new TempDataDictionary(http, new FakeTempDataProvider());
    controller.Url = new StubUrlHelper(controller.ControllerContext);
  }

  private sealed class FakeTempDataProvider : ITempDataProvider
  {
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
  }

  private sealed class StubUrlHelper : IUrlHelper
  {
    public StubUrlHelper(ActionContext actionContext) => ActionContext = actionContext;
    public ActionContext ActionContext { get; }
    public string? Action(UrlActionContext actionContext) => "/";
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => true;
    public string? Link(string? routeName, object? values) => "/";
    public string? RouteUrl(UrlRouteContext routeContext) => "/";
  }

  private sealed class FakeWebHostEnvironment : IWebHostEnvironment
  {
    public string ApplicationName { get; set; } = "Application.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
