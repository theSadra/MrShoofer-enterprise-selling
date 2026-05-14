using Application.Data;
using Application.Models;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.ViewModels;
using Application.ViewModels.Admin.AgecyManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Areas.Admin.Controllers
{
  [Area("Admin")]
  [Route("[area]/Agency/[action]")]
  [Authorize(Policy = "Admin")]
  public class AgencyManagement : Controller
  {
    private readonly AppDbContext context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient apiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgencyManagement> _logger;

    public AgencyManagement(
      AppDbContext context,
      UserManager<IdentityUser> userManager,
      MrShooferAPIClient client,
      IConfiguration configuration,
      ILogger<AgencyManagement> logger)
    {
      this.apiClient = client;
      this.context = context;
      this._userManager = userManager;
      this._configuration = configuration;
      this._logger = logger;
    }

    [Route("/Admin/")]
    public IActionResult Index()
    {
      return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
      return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAgencyViewModel viewModel)
    {

      if (!ModelState.IsValid)
      {
        ViewBag.status = "error";
        ViewBag.message = "ورودی ها را دوباره برسی کنید";
        return View("Index", viewModel);
      }

      // Creating an identityUser for new Agency

      IdentityUser identityuser = new IdentityUser()
      {
        UserName = viewModel.Username,
        PhoneNumber = viewModel.AdminMobile
      };

      // Registering agency as OTASeller in ORS

      var sellerToken = _configuration["MrShoofer:SellerToken"];
      if (string.IsNullOrWhiteSpace(sellerToken))
      {
        TempData["status"] = "error";
        TempData["message"] = "توکن فروشنده برای ثبت OTA تنظیم نشده است";
        return RedirectToAction("Index");
      }

      apiClient.SetSellerApiKey(sellerToken);

      var createOTADTO = new RegisterOTADTO()
      {
        Username = viewModel.Username,
        Password = viewModel.Password,
        BackupNumberPhone = viewModel.PhoneNumber,
        BaseCommission = viewModel.Commission,
        EmailAdress = $"{viewModel.Username}@agency.local",
        CompanyAddress = viewModel.Address,
        CompanyName = viewModel.Name,
        NumberPhone = viewModel.AdminMobile
      };
      string apikey;
      try
      {
        apikey = await apiClient.RegisterOTA(createOTADTO);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to register OTA for username {Username}", viewModel.Username);
        TempData["status"] = "error";
        TempData["message"] = "ثبت OTA ناموفق بود: " + ex.Message;
        return RedirectToAction("Index");
      }

      var result = await _userManager.CreateAsync(identityuser, viewModel.Password);


      if (!result.Succeeded)
      {
        var identityErrors = string.Join(" | ", result.Errors.Select(e => e.Description));
        TempData["status"] = "error";
        TempData["message"] = string.IsNullOrWhiteSpace(identityErrors)
          ? "در طی فرایند مشکلی وجود دارد"
          : identityErrors;
        return RedirectToAction("Index");
      }


      // Adding Agency claim to Agency identity user
      await _userManager.AddClaimAsync(identityuser, new System.Security.Claims.Claim("Role", "Agency"));

      Agency agency = new Agency()
      {
        Name = viewModel.Name,
        AdminMobile = viewModel.AdminMobile,
        DateJoined = DateTime.Now,
        Address = viewModel.Address,
        Commission = viewModel.Commission,
        PhoneNumber = viewModel.PhoneNumber,
        IdentityUser = identityuser,
        ORSAPI_token = apikey
      };

      context.Agencies.Add(agency);
      await context.SaveChangesAsync();

      TempData["status"] = "success";
      TempData["message"] = "فروشنده با موفقیت ثبت شد";

      return RedirectToAction("Index");
    }

    // Get DetailOverview
    public async Task<IActionResult> DetailOverview([FromQuery] int id)
    {
      var agency = context.Agencies.Where(a => a.Id == id)
        .FirstOrDefault();


      if (agency == null)
      {
        return NotFound();
      }

      await context.Entry(agency)
         .Reference(a => a.IdentityUser)
         .LoadAsync();

      // Filling viewbags

      ViewBag.totalsoled = context.Tickets
    .Where(t => t.Agency == agency)
    .Count();

      ViewBag.balance = (await GetAgencyBalance(agency.ORSAPI_token)).ToString("N0");

      return View(agency);
    }

    public async Task<IActionResult> Security([FromQuery] int id)
    {
      var agency = context.Agencies.Where(a => a.Id == id)
        .FirstOrDefault();
      if (agency == null)
      {
        return NotFound();
      }
      await context.Entry(agency)
         .Reference(a => a.IdentityUser)
         .LoadAsync();
      return View(agency);
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(int id, string newPassword)
    {
      var agency = context.Agencies
          .Include(a => a.IdentityUser)
          .FirstOrDefault(a => a.Id == id);

      if (agency == null)
      {
        return NotFound();
      }

      var user = agency.IdentityUser;
      if (user == null)
      {
        TempData["status"] = "error";
        TempData["message"] = "کاربر مربوط به فروشنده یافت نشد";
        return RedirectToAction("Security", new { id = agency.Id });
      }

      // Remove the existing password
      var removePasswordResult = await _userManager.RemovePasswordAsync(user);
      if (!removePasswordResult.Succeeded)
      {
        TempData["status"] = "error";
        TempData["message"] = "در طی فرایند حذف رمز عبور مشکلی پیش آمد";
        return RedirectToAction("Security", new { id = agency.Id });
      }

      // Add the new password
      var addPasswordResult = await _userManager.AddPasswordAsync(user, newPassword);
      if (!addPasswordResult.Succeeded)
      {
        TempData["status"] = "error";
        TempData["message"] = "در طی فرایند تغییر رمز عبور مشکلی پیش آمد";
        return RedirectToAction("Security", new { id = agency.Id });
      }

      TempData["status"] = "success";
      TempData["message"] = "رمز عبور با موفقیت تغییر یافت";
      return RedirectToAction("Security", new { id = agency.Id });
    }

    public async Task<IActionResult> GetAgenciesJson()
    {
      var agecyResult = context.Agencies
        .AsNoTracking()
        .Select(a => new
        {
          id = a.Id,
          name = a.Name,
          admin_phone = a.AdminMobile,
          allsoled = a.SoldTickets.Count(),
          address = a.Address
        }).ToList();

      return Json(new { data = agecyResult });
    }

    [NonAction]
    private async Task<int> GetAgencyBalance(string api_token)
    {
      apiClient.SetSellerApiKey(api_token);

      return (int)Convert.ToDouble(await apiClient.GetAccountBalance());
    }

    public async Task<IActionResult> GetAgencyTickets(int id)
    {
      var tickets = context.Tickets.Where(t => t.Agency.Id == id)
        .OrderByDescending(t => t.RegisteredAt)
        .Select(t => new
        {
          id = t.Id,
          code = t.TicketCode,
          firstname = t.Firstname,
          lastname = t.Lastname,
          price = t.TicketFinalPrice.ToString("N0"),
          registeredAt_date = t.RegisteredAt.ToPersianDate().Day + " " + t.RegisteredAt.ToPersianDate().MonthName + " " + t.RegisteredAt.ToPersianDate().Year,
          registeredAt_time = t.RegisteredAt.ToString("HH:mm:ss"),
          phonenumber = t.PhoneNumber,
          origin = t.TripOrigin,
          dest = t.TripDestination,
          cancelled = t.IsCancelled,
          carname = t.CarName,
          servicename = t.ServiceName
        }).ToList();

      return Json(new { data = tickets });
    }


    [HttpPost]
    public async Task<IActionResult> ChargeAgencyBalance(ChargeAgencyBalanceViewModel viewmodel)
    {
      if (!ModelState.IsValid)
      {
        return RedirectToAction("DetailOverview", new { id = viewmodel.AgencyId });
      }


      var agency = context.Agencies.Where(a => a.Id == viewmodel.AgencyId).FirstOrDefault();


      if (agency == null)
        return BadRequest();


      var amount = int.Parse(viewmodel.Amount.Replace(",", ""));
      try
      {
        apiClient.SetSellerApiKey(agency.ORSAPI_token);
        await apiClient.ChargeOTABalanceAsync(amount);
      }


      catch (Exception ex)
      {
        return BadRequest(ex.Message);
      }

      AgencyBalanceCharge abc = new AgencyBalanceCharge
      {
        Agency = agency,
        ChargedAt = DateTime.Now,
        Description = viewmodel.Description,
        Amount = amount,
        PaymentID = viewmodel.PayemntId
      };

      context.AgencyBalanceCharges.Add(abc);


      await context.SaveChangesAsync();



      TempData["status"] = "success";
      TempData["message"] = "حساب اعتبار فروشنده‌ی" + $" {agency.Name} " + "با موفقیت به مبلغ" + $" {amount.ToString("N0")} ، شارژ گردید";

      return RedirectToAction("DetailOverview", new { id = agency.Id });
    }
  }
}
