using Application.Data;
using Application.Migrations;
using Application.Services;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;
using Application.Models;
using Application.Utilities;
using Microsoft.AspNetCore.Authorization;
using Application.ViewModels.TaxiTrips;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  // Removed [Authorize] - Allow guest access to search trips
  public class TaxiTripsController : Controller
  {
    private readonly DirectionsRepository directionsRepository;
    private readonly MrShooferAPIClient _mrShooferAPIClient;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppDbContext context;
    private readonly DirectionsTravelTimeCalculator _travelTimeCalculator;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    private Agency? agency;

    // Clean single constructor
    public TaxiTripsController(
      DirectionsRepository directionsRepository,
      MrShooferAPIClient mrShooferAPIClient,
      UserManager<IdentityUser> userManager,
      AppDbContext context,
      DirectionsTravelTimeCalculator calculator,
      IConfiguration configuration,
      IWebHostEnvironment env)
    {
      this.context = context;
      _userManager = userManager;
      _mrShooferAPIClient = mrShooferAPIClient;
      this.directionsRepository = directionsRepository;
      this._travelTimeCalculator = calculator;
      _configuration = configuration;
      _env = env;
    }

    private static string NormalizeCity(string? s)
    {
      if (string.IsNullOrWhiteSpace(s)) return string.Empty;
      var str = s.Trim();
      var idx = str.IndexOf('(');
      if (idx >= 0) str = str[..idx];
      str = Regex.Replace(str, "[\u200C\u200F\u200E\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED]", string.Empty);
      str = str.Replace('\u064A', '\u06CC').Replace('\u0643', '\u06A9');
      str = str.Replace('\u0629', '\u0647');
      str = Regex.Replace(str, "\u0020+", " ").ToLowerInvariant();
      return str;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string originstring, string destinationstring, string searchdate)
    {
      if (string.IsNullOrWhiteSpace(originstring) || string.IsNullOrWhiteSpace(destinationstring))
      {
        ModelState.AddModelError(string.Empty, "لطفا شهر مبدا و مقصد را وارد کنید.");
        ViewBag.origin_city_text = originstring;
        ViewBag.dest_city_text = destinationstring;
        ViewBag.searchdate = searchdate;
        return View();
      }

      var allDirections = directionsRepository.GetDirections();
      var normMap = allDirections.ToDictionary(k => NormalizeCity(k.Key), v => v.Value);

      var originKey = NormalizeCity(originstring);
      var destKey = NormalizeCity(destinationstring);

      int origin_id = 0, destination_id = 0;

      // First try static map
      normMap.TryGetValue(originKey, out origin_id);
      normMap.TryGetValue(destKey, out destination_id);

      // If either ID is missing, try the dynamic API map
      if (origin_id == 0 || destination_id == 0)
      {
        try
        {
          var dynamicMap = await _mrShooferAPIClient.GetCityNameIdMapAsync();
          if (origin_id == 0) dynamicMap.TryGetValue(originKey, out origin_id);
          if (destination_id == 0) dynamicMap.TryGetValue(destKey, out destination_id);
        }
        catch { /* ignore SSL/map issues */ }
      }

      // If still missing, try to get direction info directly from the available directions
      if (origin_id == 0 || destination_id == 0)
      {
        try
        {
          var availableDirections = await _mrShooferAPIClient.GetAvaiableOTADirectionsAsync();
          foreach (var dir in availableDirections)
          {
            var dirOriginKey = NormalizeCity(dir.Cityone);
            var dirDestKey = NormalizeCity(dir.Citytwo);

            if (origin_id == 0 && dirOriginKey == originKey && dir.CityoneId.HasValue)
              origin_id = dir.CityoneId.Value;
            if (origin_id == 0 && dirDestKey == originKey && dir.CitytwoId.HasValue)
              origin_id = dir.CitytwoId.Value;
            if (destination_id == 0 && dirOriginKey == destKey && dir.CityoneId.HasValue)
              destination_id = dir.CityoneId.Value;
            if (destination_id == 0 && dirDestKey == destKey && dir.CitytwoId.HasValue)
              destination_id = dir.CitytwoId.Value;

            if (origin_id != 0 && destination_id != 0) break;
          }
        }
        catch { /* ignore */ }
      }

      if (origin_id == 0 || destination_id == 0)
      {
        if (origin_id == 0) ModelState.AddModelError(nameof(originstring), $"شهر مبدا نامعتبر است: {originstring}");
        if (destination_id == 0) ModelState.AddModelError(nameof(destinationstring), $"شهر مقصد نامعتبر است: {destinationstring}");
        ViewBag.origin_city_text = originstring;
        ViewBag.dest_city_text = destinationstring;
        ViewBag.searchdate = searchdate;
        return View();
      }

      PersianDate pd;
      try
      {
        pd = new PersianDate(searchdate?.Replace('-', '/') ?? string.Empty);
      }
      catch
      {
        ModelState.AddModelError(nameof(searchdate), "تاریخ نامعتبر است.");
        ViewBag.origin_city_text = originstring;
        ViewBag.dest_city_text = destinationstring;
        ViewBag.searchdate = searchdate;
        return View();
      }

      DateTime searchedDatetime = pd.ToDateTime();

      ViewBag.origin_city_text = originstring;
      ViewBag.dest_city_text = destinationstring;
      ViewBag.searchdate = searchdate;
      ViewBag.selecteddate = searchedDatetime;
      ViewBag.searchpdate = pd;

      return View();
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/TaxiTrips/AvailableDirections")]
    public async Task<IActionResult> AvailableDirection()
    {
      try
      {
        var dirs = await _mrShooferAPIClient.GetAvaiableOTADirectionsAsync();
        return Json(dirs);
      }
      catch (Exception ex)
      {
        try
        {
          var path = Path.Combine(_env.WebRootPath, "json", "Directions", "Directions.json");
          if (System.IO.File.Exists(path))
          {
            var json = System.IO.File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var list = new List<object>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
              var c1 = el.GetProperty("Cityone").GetString();
              var c2 = el.GetProperty("Citytwo").GetString();
              if (!string.IsNullOrWhiteSpace(c1) && !string.IsNullOrWhiteSpace(c2))
                list.Add(new { Cityone = c1, Citytwo = c2 });
            }
            return Json(list);
          }
        }
        catch { }
        return StatusCode(500, new { error = "Failed to load directions", detail = ex.Message });
      }
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/TaxiTrips/SupportedCities")]
    public IActionResult SupportedCities() => Json(directionsRepository.GetDirections().Keys.ToList());

    [HttpGet]
    [AllowAnonymous]
    [Route("/TaxiTrips/SearchHints")]
    public IActionResult SearchHints()
    {
      var supported = directionsRepository.GetDirections().Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
      var popularOrigins = new[] { "تهران", "اصفهان", "شیراز", "رشت", "چالوس", "کرمانشاه", "نوشهر" };
      var version = $"v2-{supported.Count}-{string.Join(',', popularOrigins)}";
      return Json(new
      {
        supportedCities = supported,
        popularOrigins,
        version
      });
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);
      string? tokenToUse = null;

      // Use agency token if authenticated, otherwise use guest/default token
      if (User?.Identity?.IsAuthenticated == true)
      {
        agency = ResolveAgencyForUser();
        if (agency != null && !string.IsNullOrWhiteSpace(agency.ORSAPI_token))
          tokenToUse = agency.ORSAPI_token;
      }

      // Fallback to guest token from configuration
      if (string.IsNullOrWhiteSpace(tokenToUse))
        tokenToUse = _configuration["MrShoofer:SellerToken"];

      if (!string.IsNullOrWhiteSpace(tokenToUse))
        _mrShooferAPIClient.SetSellerApiKey(tokenToUse);
    }

    [Route("/TaxiTrips/SearchJson")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SearchTripsJson(string originstring, string destinationstring, string searchdate)
    {
      if (string.IsNullOrWhiteSpace(originstring) || string.IsNullOrWhiteSpace(destinationstring))
        return BadRequest(new { error = "originstring and destinationstring are required." });

      var allDirections = directionsRepository.GetDirections();
      var normMap = allDirections.ToDictionary(k => NormalizeCity(k.Key), v => v.Value);
      var originKey = NormalizeCity(originstring);
      var destKey = NormalizeCity(destinationstring);

      int origin_id = 0, destination_id = 0;

      // First try static map
      normMap.TryGetValue(originKey, out origin_id);
      normMap.TryGetValue(destKey, out destination_id);

      // If either ID is missing, try the dynamic API map
      if (origin_id == 0 || destination_id == 0)
      {
        try
        {
          var dynamicMap = await _mrShooferAPIClient.GetCityNameIdMapAsync();
          if (origin_id == 0) dynamicMap.TryGetValue(originKey, out origin_id);
          if (destination_id == 0) dynamicMap.TryGetValue(destKey, out destination_id);
        }
        catch (Exception ex)
        {
          // Log but continue - we might still have one ID from static map
          System.Diagnostics.Debug.WriteLine($"Failed to get dynamic city map: {ex.Message}");
        }
      }

      // If still missing, try to get direction info directly from the available directions
      if (origin_id == 0 || destination_id == 0)
      {
        try
        {
          var availableDirections = await _mrShooferAPIClient.GetAvaiableOTADirectionsAsync();
          foreach (var dir in availableDirections)
          {
            var dirOriginKey = NormalizeCity(dir.Cityone);
            var dirDestKey = NormalizeCity(dir.Citytwo);

            if (origin_id == 0 && dirOriginKey == originKey && dir.CityoneId.HasValue)
              origin_id = dir.CityoneId.Value;
            if (origin_id == 0 && dirDestKey == originKey && dir.CitytwoId.HasValue)
              origin_id = dir.CitytwoId.Value;
            if (destination_id == 0 && dirOriginKey == destKey && dir.CityoneId.HasValue)
              destination_id = dir.CityoneId.Value;
            if (destination_id == 0 && dirDestKey == destKey && dir.CitytwoId.HasValue)
              destination_id = dir.CitytwoId.Value;

            if (origin_id != 0 && destination_id != 0) break;
          }
        }
        catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine($"Failed to get available directions for ID lookup: {ex.Message}");
        }
      }

      if (origin_id == 0)
      {
        var suggestions = allDirections.Keys.Where(k => NormalizeCity(k).Contains(originKey)).Take(5);
        return BadRequest(new { error = $"شهر مبدا نامعتبر است: {originstring}", suggestions });
      }
      if (destination_id == 0)
      {
        var suggestions = allDirections.Keys.Where(k => NormalizeCity(k).Contains(destKey)).Take(5);
        return BadRequest(new { error = $"شهر مقصد نامعتبر است: {destinationstring}", suggestions });
      }

      PersianDate pd;
      try { pd = new PersianDate(searchdate?.Replace('-', '/') ?? string.Empty); }
      catch { return BadRequest(new { error = "تاریخ نامعتبر" }); }
      DateTime searchedDatetime = pd.ToDateTime();

      List<SearchedTrip> response;
      try
      {
        response = (await _mrShooferAPIClient.SearchTrips(searchedDatetime, searchedDatetime.AddDays(1), origin_id, destination_id))?.ToList() ?? new List<SearchedTrip>();
      }
      catch (Exception ex)
      {
        // Graceful degradation: return empty array instead of 500
        return Json(new List<SearchedTripViewModel>()); // frontend shows "سفری پیدا نشد"
      }

      int traveltime_mins = _travelTimeCalculator.GetTravelMins(originstring, destinationstring);
      // Guests always see 0% commission pricing (قبل/بعد تخفیف). Logged-in agencies use live ORS %.
      agency = User?.Identity?.IsAuthenticated == true
        ? (agency ?? ResolveAgencyForUser())
        : null;
      var commissionPercent = await ResolveCommissionPercentAsync();

      var end_result = response
        .OrderBy(t => t.startingDateTime)
        .ThenBy(t => AgencyCommissionPricing.NetPayableTomans(t.afterdiscticketprice, commissionPercent))
        .Where(t => t.startingDateTime > DateTime.Now.AddMinutes(45))
        .ToList();

      var searchedTripViewModels = end_result.Select(t =>
      {
        // Recommended sale = ORS after-discount list price (charge passenger this).
        var recommendedSale = t.afterdiscticketprice;
        // Payable = after-discount minus ORS/base commission (equals list price when commission is 0).
        var payable = AgencyCommissionPricing.NetPayableTomans(recommendedSale, commissionPercent);
        var hasCommission = commissionPercent > 0;
        return new SearchedTripViewModel
        {
          startingDateTime = t.startingDateTime.ToString("HH:mm"),
          arrivalDateTime = t.startingDateTime.AddMinutes(traveltime_mins).ToString("HH:mm"),
          origin = $"{t.originCityName}({t.oringinLocationName})",
          destination = $"{t.destinationCityName}({t.destinationLocationName})",
          originalPrice = t.originalTicketprice.ToString("N0"),
          afterdiscount = recommendedSale.ToString("N0"),
          payablePrice = payable.ToString("N0"),
          commissionPercent = commissionPercent,
          hasCommission = hasCommission,
          taxiSupervisorName = t.taxiSupervisorName,
          taxiSupervisorID = t.taxiSupervisorID,
          tripcode = t.tripPlanCode,
          carModelName = t.carModelName,
          Image = _mrShooferAPIClient.ResolveMediaUrl(t.Image)
        };
      }).ToList();

      return Json(searchedTripViewModels);
    }

    private Agency? ResolveAgencyForUser()
    {
      if (User?.Identity?.IsAuthenticated != true) return null;

      var identityUser = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
      if (identityUser == null) return null;

      return context.Agencies.FirstOrDefault(a => a.IdentityUserId == identityUser.Id)
             ?? context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);
    }

    /// <summary>
    /// Prefer live ORS <c>baseCommission</c> so payable tracks ORS changes; fall back to local Agency.Commission.
    /// Guests / unauthenticated users always get 0% — show ORS قبل/بعد تخفیف (same as zero-commission agencies).
    /// </summary>
    private async Task<int> ResolveCommissionPercentAsync()
    {
      if (User?.Identity?.IsAuthenticated != true || agency == null)
        return 0;

      try
      {
        var orsInfo = await _mrShooferAPIClient.GetMyAgencyInfoAsync();
        if (orsInfo != null)
        {
          var live = (int)Math.Round(orsInfo.BaseCommission, MidpointRounding.AwayFromZero);
          if (live < 0) live = 0;
          if (live > 100) live = 100;

          // Keep local copy aligned when ORS commission changes.
          if (agency.Commission != live)
          {
            agency.Commission = live;
            try { await context.SaveChangesAsync(); } catch { /* non-fatal */ }
          }
          return live;
        }
      }
      catch { /* fall back to local */ }

      return agency.Commission;
    }
  }
}
