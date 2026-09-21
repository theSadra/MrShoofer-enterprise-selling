using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Application.Models;
using Microsoft.AspNetCore.Identity;
using Humanizer;
using Application.Data;
using System.Security.Claims;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  // Removed [Authorize] - Allow guest access to home page
  public class HomeController : Controller
  {
    private readonly ILogger<HomeController> _logger;

    private readonly UserManager<IdentityUser> userManager;

    private readonly AppDbContext context;

    private readonly SignInManager<IdentityUser> signInManager;


    public HomeController(ILogger<HomeController> logger, UserManager<IdentityUser> userManager, AppDbContext context, SignInManager<IdentityUser> signInManager)
    {
      this.context = context;
      _logger = logger;
      this.userManager = userManager;
      this.signInManager = signInManager;
    }


    public IActionResult Index()
    {
      return View();
    }
    
    public IActionResult Privacy()
    {
      return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
      // Always send users to the branded Persian error page — never the stock ASP.NET template.
      return Redirect("/Error/500");
    }
  }
}
