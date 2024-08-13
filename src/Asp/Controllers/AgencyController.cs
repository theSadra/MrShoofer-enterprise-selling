using Application.Data;
using Application.Services;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Controllers
{
  [Authorize]
  public class AgencyController : Controller
  {
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient _apiClient;
    private readonly AppDbContext _context;
    private Agency agency;

    public AgencyController(AppDbContext context, UserManager<IdentityUser> userManager, MrShooferAPIClient apiClient)
    {
      _context = context;
      _userManager = userManager;
      _apiClient = apiClient;
    }

    // Main agency page, general info last tickets
    [HttpGet]
    public async Task<IActionResult> Index()
    {
      ViewBag.agency = agency;

      _context.Entry(agency)
        .Collection(a => a.SoldTickets)
        .Load();

      AgencyAnalyzerService ana = new AgencyAnalyzerService(agency);

      var total = ana.GetThisMonthSoldTotalPrice();
      var totalprofit = ana.GetThisMonthTotalProfit();




      ViewBag.agancy_balance = (long)Convert.ToDecimal(await _apiClient.GetAccountBalance());

      // loading and fetching TODAY sold group
     await _context.Entry(agency)
        .Collection(a => a.SoldTickets)
        .LoadAsync();
      ViewBag.today_soldTickets = agency.SoldTickets
        .Where(t => t.RegisteredAt >= DateTime.Today && t.RegisteredAt < DateTime.Today.AddDays(1))
        .ToList();


      return View();
    }














    // For setting api key and getting agency entity related to current request from database
    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      var identityUser = _userManager.GetUserAsync(User).Result;
      this.agency = this._context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);

      this._apiClient.SetSellerApiKey(this.agency.ORSAPI_token);
    }

  }
}
