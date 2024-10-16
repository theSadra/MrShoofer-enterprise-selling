using Application.Data;
using Application.Migrations;
using Application.Services;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;

namespace Application.Controllers
{
  public class TaxiTripsController : Controller
  {
    private readonly DirectionsRepository directionsRepository;
    private readonly MrShooferAPIClient _mrShooferAPIClient;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppDbContext context;

    private Agency agency;


    public TaxiTripsController(DirectionsRepository directionsRepository, MrShooferAPIClient mrShooferAPIClient, UserManager<IdentityUser> userManager, AppDbContext context)
    {
      this.context = context;
      this._userManager = userManager;
      this._mrShooferAPIClient = mrShooferAPIClient;
      this.directionsRepository = directionsRepository;

    }


    // todo: need for adding mrshooferORS client to ioc service and injecting that to this class
    // todo: need for logging the search info 



    public async Task<IActionResult> Index(string originstring, string destinationstring, string searchdate)
    {

      // Getting origin and destination ID 
      int origin_id = directionsRepository.GetDirections()[originstring];
      int destination_id = directionsRepository.GetDirections()[destinationstring];


      // Translating entered SHAMSI search date to DateTime object
      PersianDate pd = new PersianDate(searchdate.Replace('-', '/'));
      DateTime searchedDatetime = pd.ToDateTime();



      // Fetching trips based on search parameters from ORS
      var response = (await _mrShooferAPIClient.SearchTrips(searchedDatetime, searchedDatetime.AddDays(1), origin_id, destination_id));
      // Filling viewbags

      ViewBag.origin_city_text = originstring;
      ViewBag.dest_city_text = destinationstring;
      ViewBag.searchdate = searchdate;
      ViewBag.selecteddate = searchedDatetime;

      return View(response
        .OrderBy(t => t.startingDateTime).ToList());
    }



    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      var identityUser = _userManager.GetUserAsync(User).Result;
      this.agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);


      this._mrShooferAPIClient.SetSellerApiKey(this.agency.ORSAPI_token);
    }
  }
}
