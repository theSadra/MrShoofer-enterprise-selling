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
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Application.ViewModels.TaxiTrips;


namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize]
  public class TaxiTripsController : Controller
  {
    private readonly DirectionsRepository directionsRepository;
    private readonly MrShooferAPIClient _mrShooferAPIClient;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppDbContext context;
    private readonly DirectionsTravelTimeCalculator _travelTimeCalculator;

    private Agency agency;


    public TaxiTripsController(DirectionsRepository directionsRepository, MrShooferAPIClient mrShooferAPIClient, UserManager<IdentityUser> userManager, AppDbContext context, DirectionsTravelTimeCalculator calculator)
    {
      this.context = context;
      _userManager = userManager;
      _mrShooferAPIClient = mrShooferAPIClient;
      this.directionsRepository = directionsRepository;
      this._travelTimeCalculator = calculator;

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
      var response = await _mrShooferAPIClient.SearchTrips(searchedDatetime, searchedDatetime.AddDays(1), origin_id, destination_id);
      // Filling viewbags

      ViewBag.origin_city_text = originstring;
      ViewBag.dest_city_text = destinationstring;
      ViewBag.searchdate = searchdate;
      ViewBag.selecteddate = searchedDatetime;
      ViewBag.searchpdate = pd;


      return View();
    }


    [Route("/TaxiTrips/SearchJson")]
    public async Task<IActionResult> SearchTripsJson(string originstring, string destinationstring, string searchdate)
    {

      // Getting origin and destination ID 
      int origin_id = directionsRepository.GetDirections()[originstring];
      int destination_id = directionsRepository.GetDirections()[destinationstring];


      // Translating entered SHAMSI search date to DateTime object
      PersianDate pd = new PersianDate(searchdate.Replace('-', '/'));
      DateTime searchedDatetime = pd.ToDateTime();



      // Fetching trips based on search parameters from ORS

      var response = await _mrShooferAPIClient.SearchTrips(searchedDatetime, searchedDatetime.AddDays(1), origin_id, destination_id);


      int traveltime_mins = _travelTimeCalculator.GetTravelMins(originstring, destinationstring);


      var end_result = response
          .OrderBy(t => t.startingDateTime)
          .ThenBy(t => t.afterdiscticketprice)
          .ToList();

      // Removing outdated trips
      end_result.RemoveAll(t => t.startingDateTime <= DateTime.Now.AddMinutes(45));

      // Generating respose viewmodels
      var searchedTripViewModels = end_result.Select(t => new SearchedTripViewModel()
      {
        startingDateTime = t.startingDateTime.ToString("HH:mm"),
        arrivalDateTime = t.startingDateTime.AddMinutes(traveltime_mins).ToString("HH:mm"),
        origin = $"{t.originCityName}({t.oringinLocationName})",
        destination = $"{t.destinationCityName}({t.destinationLocationName})",
        originalPrice = t.originalTicketprice.ToString("N0"),
        afterdiscount = t.afterdiscticketprice.ToString("N0"),
        taxiSupervisorName = t.taxiSupervisorName,
        taxiSupervisorID = t.taxiSupervisorID,
        tripcode = t.tripPlanCode,
        carModelName = t.carModelName
      })
       .ToList();


      return Json(searchedTripViewModels);
    }




    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);

      var identityUser = _userManager.GetUserAsync(User).Result;
      agency = this.context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);


      _mrShooferAPIClient.SetSellerApiKey(agency.ORSAPI_token);
    }
  }
}
