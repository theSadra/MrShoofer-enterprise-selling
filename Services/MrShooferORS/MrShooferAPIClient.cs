using Application.ViewModels;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Services.MrShooferORS
{
  public class MrShooferAPIClient
  {
    string _apikey;
    readonly HttpClient _client;
    readonly string _sellerapikey;

    public MrShooferAPIClient(HttpClient client, string baseurl)
    {
      _client = client;

      _client.BaseAddress = new Uri(baseurl);
    }


    public void SetSellerApiKey(string apikey)
    {
      this._apikey = apikey;
      _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this._apikey);

    }


    public static async Task<string> GetSellerApiKey_LoginAsync(string username, string password)
    {

      HttpClient loginclient = new HttpClient();
      loginclient.BaseAddress = new Uri("https://mrbilit.mrshoofer.ir");


      var result = await loginclient.GetAsync($"/Account/Login?adminnumberphone={username}&password={password}");
      var node = JsonNode.Parse(await result.Content.ReadAsStringAsync());

      return node["token"].ToString();
    }


    public async Task<string> GetAccountBalance()
    {
      var result = await _client.GetAsync("/Account/getAccountBalance");


      var node = JsonNode.Parse(await result.Content.ReadAsStringAsync());

      return node["accountBalance_tomans"].ToString();

    }


    public async Task<IList<SearchedTrip>> SearchTrips(DateTime startspan, DateTime endspan, int originCityId, int destinationCityid, int? originterminalId = null, int? destinationterminalid = null)
    {
      string searchurl = $"https://mrbilit.mrshoofer.ir/Trips/GetPlanedTripsbyCityID/{startspan.ToString("yyyy-MM-dd")}/{endspan.ToString("yyyy-MM-dd")}/{originCityId}/{destinationCityid}";


      if (originterminalId != null)
      {
        searchurl += $"/{originterminalId}";
      }
      if (destinationterminalid != null)
      {
        searchurl += $"/{destinationterminalid}";
      }


      var searchedtrips = await _client.GetFromJsonAsync<List<SearchedTrip>>(searchurl);


      return searchedtrips;
    }

    public async Task<SearchedTrip> GetTripInfo(string tripcode)
    {

      string searchurl = $"https://mrbilit.mrshoofer.ir/Trips/getTripinfo?tripcode={tripcode}";
      var result = await _client.GetFromJsonAsync<SearchedTrip>(searchurl);

      if (result == null)
      {
        throw new Exception("Trip not found");
      }


      return result;
    }


    /// <summary>
    /// Reserves temporarirly the ticket for trip
    /// </summary>
    /// <param name="ticket">ticket needs for temporarirly reserved</param>
    /// <returns>Temporarirly reservatoin code</returns>
    public async Task<string> ReserveTicketTemporarirly(TicketTempReserveRequestModel ticket)
    {
      var response = await _client.PostAsJsonAsync<TicketTempReserveRequestModel>("/Tickets/reserverTemporarily", ticket);

      if ((int)response.StatusCode != 200)
        throw new Exception();


      var jsonresult = await response.Content.ReadAsStringAsync();

      var node = JsonNode.Parse(jsonresult);


      return node["ticketCode"].ToString();

    }

    public async Task<TicketConfirmationResponse> ConfirmReserve(ConfirmReserveRequestModel confirmreservemodel)
    {
      var response = await _client.PostAsJsonAsync<ConfirmReserveRequestModel>("https://mrbilit.mrshoofer.ir/Tickets/confirmReserve", confirmreservemodel);

      // When error happend
      if ((int)response.StatusCode != 200)
      {
        var jsonresult = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        throw new Exception(jsonresult["error"].ToString());
      }


      var jsonresponse = JsonNode.Parse(await response.Content.ReadAsStringAsync());

      var confirmationmodel = JsonSerializer.Deserialize<TicketConfirmationResponse>(jsonresponse);

      return confirmationmodel;
    }


    public async Task<string> RegisterOTA(RegisterOTADTO registerOTADTO)
    {
      string url = "https://mrbilit.mrshoofer.ir/OTAManagement/RegisterNewOTA";



      var result = await _client.PostAsJsonAsync(url, registerOTADTO);
      if (!result.IsSuccessStatusCode)
      {
        throw new Exception();
      }

      return await result.Content.ReadAsStringAsync();
    }


    public async Task ChargeOTABalanceAsync(int amount)
    {
      var content = new StringContent($"charge_amount={amount}", Encoding.UTF8, "application/x-www-form-urlencoded");

      // Make the POST request
      var response = await _client.PostAsync("https://mrbilit.mrshoofer.ir/OTAManagement/ChargeOTA", content);
      if (!response.IsSuccessStatusCode)
      {
        throw new Exception();
      }
    }
  }
}
