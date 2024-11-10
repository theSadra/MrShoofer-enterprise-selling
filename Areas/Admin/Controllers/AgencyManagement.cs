using Application.Data;
using Application.Models;
using Application.Services.MrShooferORS;
using Application.ViewModels;
using Application.ViewModels.Admin.AgecyManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        return View("Index",viewModel);
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
  }
}
