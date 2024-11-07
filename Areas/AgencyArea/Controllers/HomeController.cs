using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Humanizer;
using Application.Data;
using System.Security.Claims;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize(Policy = "Admin")]
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
      IdentityUser adminIdentity = userManager.FindByNameAsync("shooferadmin").Result;

       var result =  signInManager.PasswordSignInAsync(adminIdentity,"ShooferGuy1403",true,true).Result;
      

      context.SaveChanges();




      return View();
    }
    public IActionResult Privacy()
    {
      return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
  }
}
