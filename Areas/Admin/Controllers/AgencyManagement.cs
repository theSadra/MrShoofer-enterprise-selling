using Application.Data;
using Application.Models;
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


    public AgencyManagement(AppDbContext context, UserManager<IdentityUser> userManager)
    {
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
        return View(viewModel);
      }

      // Creating an identityUser for new Agency

      IdentityUser identityuser = new IdentityUser()
      {
        UserName = viewModel.Username,
        PhoneNumber = viewModel.AdminMobile
      };

      var result = await _userManager.CreateAsync(identityuser,viewModel.Password);


      if (!result.Succeeded)
      {
        return View(viewModel);
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
        ORSAPI_token = "TestToken"
      };


      context.Agencies.Add(agency);
      await context.SaveChangesAsync();

      return View();
    }
  }
}
