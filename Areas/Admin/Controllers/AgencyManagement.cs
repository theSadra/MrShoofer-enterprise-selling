using Application.Data;
using Application.Models;
using Application.Services;
using Application.Services.MrShooferORS;
using Application.ViewModels;
using Application.ViewModels.Admin.AgecyManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Cms.Ecc;
using System.Data.Entity;
using System.Data.Entity.Core.Objects.DataClasses;

namespace Application.Areas.Admin.Controllers
{
  [Area("Admin")]
  [Route("[area]/Agency/[action]")]
  //[Authorize(Policy = "Admin")]
  public class AgencyManagement : Controller
  {
    private readonly AppDbContext context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient apiClient;

    public AgencyManagement(AppDbContext context, UserManager<IdentityUser> userManager, MrShooferAPIClient client)
    {
      this.apiClient = client;
      this.context = context;
      this._userManager = userManager;
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

      apiClient.SetSellerApiKey("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjE3IiwianRpIjoiN2E4OWFkYzMtN2VhMC00YWE4LTg1YWUtZjg5M2RkZmI4MjVmIiwiZXhwIjoxODg4OTg2ODEzLCJpc3MiOiJtcnNob29mZXIuaXIiLCJhdWQiOiJtcnNob29mZXIuaXIifQ.uGHR7bq5eQQ6HPTW2ooskdaHdAwumfw_Rxx411NLqw4");

      var createOTADTO = new RegisterOTADTO()
      {
        Username = viewModel.Username,
        Password = viewModel.Password,
        BackupNumberPhone = viewModel.PhoneNumber,
        BaseCommission = viewModel.Commission,
        EmailAdress = "",
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
        ViewBag.status = "error";
        ViewBag.message = "در طی فرایند مشکلی پیش آمد";
        return View("Index", viewModel);
      }

      var result = await _userManager.CreateAsync(identityuser, viewModel.Password);


      if (!result.Succeeded)
      {
        ViewBag.status = "error";
        ViewBag.message = "در طی فرایند مشکلی وجود دارد";
        return View("Index", viewModel);
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

      ViewBag.status = "success";
      ViewBag.message = "فروشنده با موفقیت ثبت شد";

      return View("Index");
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

      return RedirectToAction("DetailOverview", new {id = agency.Id});
    }
  }
}
