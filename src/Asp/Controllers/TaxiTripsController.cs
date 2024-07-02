using Application.Services;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Application.Controllers
{
  public class TaxiTripsController : Controller
  {
    private readonly DirectionsRepository directionsRepository;

    public TaxiTripsController(DirectionsRepository directionsRepository)
    {
        this.directionsRepository = directionsRepository;
    }


    // todo: need for adding mrshooferORS client to ioc service and injecting that to this class
    // todo: need for logging the search info 



    public async Task<IActionResult> Index(string originstring, string destinationstring, string searchdate)
    {
      var apikey = await MrShooferAPIClient.GetSellerApiKey_LoginAsync("09132269102", "mrbilitATG9996");



      // Getting origin and destination ID 
      int origin_id = directionsRepository.GetDirections()[originstring];
      int destination_id = directionsRepository.GetDirections()[destinationstring];


      MrShooferAPIClient client = new MrShooferAPIClient(new HttpClient(), "https://mrbilit.mrshoofer.ir", apikey);


      // Translating entered SHAMSI search date to DateTime object
      PersianDate pd = new PersianDate(searchdate.Replace('-','/'));
      DateTime searchedDatetime = pd.ToDateTime();


      // Fetching trips based on search parameters from ORS
      var response = (await client.SearchTrips(searchedDatetime, searchedDatetime.AddDays(1), origin_id,destination_id))
        ;

      // Filling viewbags

      ViewBag.origin_city_text = originstring;
      ViewBag.dest_city_text = destinationstring;
      ViewBag.searchdate = searchdate;  


      return View(response
        .OrderBy(t => t.startingDateTime).ToList()
        );
    }
  }
}
