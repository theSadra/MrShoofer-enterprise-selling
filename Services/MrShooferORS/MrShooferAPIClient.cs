using Application.ViewModels;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Services.MrShooferORS
{
  public class MrShooferAPIClient
  {
    string? _apikey;
    readonly HttpClient _client;
    private static string _staticBaseUrl = "http://localhost:5000"; // updated by constructor for static login method

    // JSON serializer options with case-insensitive property matching
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    };

    public MrShooferAPIClient(HttpClient client)
    {
      _client = client;
      if (client.BaseAddress != null)
        _staticBaseUrl = client.BaseAddress.ToString();
    }


    public void SetSellerApiKey(string apikey)
    {
      this._apikey = apikey;
      _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this._apikey);

    }


    public static async Task<string> GetSellerApiKey_LoginAsync(string username, string password)
    {

      HttpClient loginclient = new HttpClient();
      loginclient.BaseAddress = new Uri(_staticBaseUrl);


      var result = await loginclient.GetAsync($"/Account/Login?adminnumberphone={username}&password={password}");
      var node = JsonNode.Parse(await result.Content.ReadAsStringAsync());

      return node?["token"]?.ToString() ?? throw new Exception("Login failed: no token in response");
    }


    public async Task<string?> GetAccountBalance()
    {
      try
      {
        var result = await _client.GetAsync("/Account/getAccountBalance");
        var body = await result.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);
        return node?["accountBalance_tomans"]?.ToString();
      }
      catch
      {
        return null;
      }
    }


    public async Task<IList<SearchedTrip>> SearchTrips(DateTime startspan, DateTime endspan, int originCityId, int destinationCityid, int? originterminalId = null, int? destinationterminalid = null)
    {
      string searchurl = $"/Trips/GetPlanedTripsbyCityID/{startspan:yyyy-MM-dd}/{endspan:yyyy-MM-dd}/{originCityId}/{destinationCityid}";


      if (originterminalId != null)
      {
        searchurl += $"/{originterminalId}";
      }
      if (destinationterminalid != null)
      {
        searchurl += $"/{destinationterminalid}";
      }


      var response = await _client.GetAsync(searchurl);
      response.EnsureSuccessStatusCode();

      var json = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<List<SearchedTrip>>(json, _jsonOptions) ?? [];
    }

    public async Task<SearchedTrip> GetTripInfo(string tripcode)
    {

      string searchurl = $"/Trips/getTripinfo?tripcode={tripcode}";

      var response = await _client.GetAsync(searchurl);
      response.EnsureSuccessStatusCode();

      var json = await response.Content.ReadAsStringAsync();
      var result = JsonSerializer.Deserialize<SearchedTrip>(json, _jsonOptions);

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


      return node?["ticketCode"]?.ToString() ?? throw new Exception("Reserve failed: no ticketCode in response");

    }

    public async Task<TicketConfirmationResponse> ConfirmReserve(ConfirmReserveRequestModel confirmreservemodel)
    {
      var response = await _client.PostAsJsonAsync<ConfirmReserveRequestModel>("/Tickets/confirmReserve", confirmreservemodel);

      // When error happend
      if ((int)response.StatusCode != 200)
      {
        var jsonresult = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        throw new Exception(jsonresult?["error"]?.ToString() ?? "ConfirmReserve failed");
      }


      var jsonresponse = JsonNode.Parse(await response.Content.ReadAsStringAsync());

      return JsonSerializer.Deserialize<TicketConfirmationResponse>(jsonresponse)
          ?? throw new Exception("ConfirmReserve: failed to deserialize response");
    }


    public async Task<string> RegisterOTA(RegisterOTADTO registerOTADTO)
    {
      string url = "/OTAManagement/RegisterNewOTA";

      // Build payload explicitly to guarantee exact property names expected by OTA API.
      var payload = new
      {
        Username = registerOTADTO.Username,
        Password = registerOTADTO.Password,
        EmailAdress = registerOTADTO.EmailAdress,
        CompanyName = registerOTADTO.CompanyName,
        NumberPhone = registerOTADTO.NumberPhone,
        BackupNumberPhone = registerOTADTO.BackupNumberPhone,
        BaseCommission = registerOTADTO.BaseCommission,
        CompanyAddress = registerOTADTO.CompanyAddress
      };

      var jsonBody = JsonSerializer.Serialize(payload, new JsonSerializerOptions
      {
        PropertyNamingPolicy = null
      });

      using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
      var result = await _client.PostAsync(url, content);
      var responseBody = await result.Content.ReadAsStringAsync();

      // Some OTA hosts bind this endpoint from form body instead of JSON.
      // If JSON binding fails with "required field" validation, retry once as form-urlencoded.
      if (!result.IsSuccessStatusCode && (int)result.StatusCode == 400 && responseBody.Contains("required", StringComparison.OrdinalIgnoreCase))
      {
        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
          ["Username"] = registerOTADTO.Username ?? string.Empty,
          ["Password"] = registerOTADTO.Password ?? string.Empty,
          ["EmailAdress"] = registerOTADTO.EmailAdress ?? string.Empty,
          ["CompanyName"] = registerOTADTO.CompanyName ?? string.Empty,
          ["NumberPhone"] = registerOTADTO.NumberPhone ?? string.Empty,
          ["BackupNumberPhone"] = registerOTADTO.BackupNumberPhone ?? string.Empty,
          ["BaseCommission"] = registerOTADTO.BaseCommission.ToString(System.Globalization.CultureInfo.InvariantCulture),
          ["CompanyAddress"] = registerOTADTO.CompanyAddress ?? string.Empty
        });

        result = await _client.PostAsync(url, formContent);
        responseBody = await result.Content.ReadAsStringAsync();
      }

      if (!result.IsSuccessStatusCode)
      {
        throw new Exception($"OTA API error {(int)result.StatusCode} {result.ReasonPhrase}: {responseBody}");
      }

      // API may return a raw token string, a JSON string token, or an object containing token/apikey.
      if (string.IsNullOrWhiteSpace(responseBody))
      {
        throw new Exception("OTA API returned an empty response body.");
      }

      try
      {
        var parsedNode = JsonNode.Parse(responseBody);

        if (parsedNode is JsonValue value && value.TryGetValue<string>(out var tokenAsString) && !string.IsNullOrWhiteSpace(tokenAsString))
        {
          return tokenAsString;
        }

        if (parsedNode is JsonObject obj)
        {
          var tokenNode = obj["token"] ?? obj["apiKey"] ?? obj["apikey"];
          if (tokenNode is JsonValue tokenValue && tokenValue.TryGetValue<string>(out var extractedToken) && !string.IsNullOrWhiteSpace(extractedToken))
          {
            return extractedToken;
          }
        }
      }
      catch
      {
        // Not JSON, continue with raw body.
      }

      return responseBody.Trim().Trim('"');
    }

    //Get available OTA directions
    public record AvaiableDirection(string Cityone, string Citytwo, int? CityoneId, int? CitytwoId);

    private static string? ExtractString(JsonNode? node)
    {
      if (node == null) return null;

      if (node is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
      {
        return s;
      }

      if (node is JsonObject jobj)
      {
        // Prioritized child property names commonly used for city labels
        var candidateChildNames = new[]
        {
          "city_name","cityName","name","title","label","fa","persian","caption","display"
        };
        foreach (var childName in candidateChildNames)
        {
          if (jobj.TryGetPropertyValue(childName, out var child) && child is JsonValue cjv && cjv.TryGetValue<string>(out var cs) && !string.IsNullOrWhiteSpace(cs))
          {
            return cs;
          }
        }
        // Fallback: scan for first string value in object
        foreach (var kv in jobj)
        {
          var inner = ExtractString(kv.Value);
          if (!string.IsNullOrWhiteSpace(inner)) return inner;
        }
        return null;
      }

      if (node is JsonArray arr)
      {
        foreach (var el in arr)
        {
          var inner = ExtractString(el);
          if (!string.IsNullOrWhiteSpace(inner)) return inner;
        }
      }

      // As a last resort
      var asStr = node.ToString();
      return string.IsNullOrWhiteSpace(asStr) ? null : asStr;
    }

    private static string? TryGetString(JsonObject obj, params string[] candidates)
    {
      foreach (var name in candidates)
      {
        // Find property case-insensitively
        var prop = obj.FirstOrDefault(kvp => string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(prop.Key) && prop.Value is not null)
        {
          var extracted = ExtractString(prop.Value);
          if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
        }
      }
      return null;
    }

    private static int? ExtractInt(JsonNode? node)
    {
      if (node == null) return null;
      if (node is JsonValue jv)
      {
        if (jv.TryGetValue<int>(out var i)) return i;
        if (jv.TryGetValue<long>(out var l)) return (int)l;
        if (jv.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) return p;
      }
      return null;
    }

    private static int? TryGetInt(JsonObject obj, params string[] candidates)
    {
      foreach (var name in candidates)
      {
        var prop = obj.FirstOrDefault(kvp => string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(prop.Key) && prop.Value is not null)
        {
          var extracted = ExtractInt(prop.Value);
          if (extracted.HasValue) return extracted.Value;
        }
      }
      return null;
    }

    public async Task<List<AvaiableDirection>> GetAvaiableOTADirectionsAsync()
    {
      string url = "/Directions/getAvailableDirections";
      using var response = await _client.GetAsync(url);
      if (!response.IsSuccessStatusCode)
      {
        var body = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to fetch available directions: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
      }

      var json = await response.Content.ReadAsStringAsync();
      var node = JsonNode.Parse(json);

      var list = new List<AvaiableDirection>();
      if (node is JsonArray arr)
      {
        foreach (var item in arr)
        {
          if (item is not JsonObject obj) continue;

          // Try common property names for origin/destination
          var c1 = TryGetString(obj,
            "Cityone", "cityone", "CityOne", "cityOne", "city_one",
            "origin", "originCity", "fromCity", "from", "startCity", "originCityName", "cityOneName",
            "city_name", "from_city", "source", "origin_city_name", "originName");
          var c2 = TryGetString(obj,
            "Citytwo", "citytwo", "CityTwo", "cityTwo", "city_two",
            "destination", "destinationCity", "toCity", "to", "endCity", "destinationCityName", "cityTwoName",
            "dest_city", "destination_city", "target", "destination_city_name", "destinationName");

          // Enhanced ID extraction with more property name candidates
          var id1 = TryGetInt(obj,
            "CityoneId", "cityoneid", "cityOneId", "CityOneId",
            "originCityId", "fromCityId", "city_one_id", "origin_city_id",
            "originId", "origin_id", "fromId", "from_id", "startCityId", "start_city_id",
            "id1", "cityId1", "city_id_1");
          var id2 = TryGetInt(obj,
            "CitytwoId", "citytwoid", "cityTwoId", "CityTwoId",
            "destinationCityId", "toCityId", "city_two_id", "destination_city_id",
            "destinationId", "destination_id", "toId", "to_id", "endCityId", "end_city_id",
            "id2", "cityId2", "city_id_2");

          // If IDs are in nested objects, try to extract them
          if (!id1.HasValue)
          {
            var originObj = TryGetObject(obj, "origin", "originCity", "from", "cityOne", "Cityone");
            if (originObj != null)
            {
              id1 = TryGetInt(originObj, "id", "cityId", "city_id", "Id", "ID");
            }
          }
          if (!id2.HasValue)
          {
            var destObj = TryGetObject(obj, "destination", "destinationCity", "to", "cityTwo", "Citytwo");
            if (destObj != null)
            {
              id2 = TryGetInt(destObj, "id", "cityId", "city_id", "Id", "ID");
            }
          }

          if (!string.IsNullOrWhiteSpace(c1) && !string.IsNullOrWhiteSpace(c2))
          {
            list.Add(new AvaiableDirection(c1!, c2!, id1, id2));
          }
        }
      }

      return list;
    }

    private static JsonObject? TryGetObject(JsonObject obj, params string[] candidates)
    {
      foreach (var name in candidates)
      {
        var prop = obj.FirstOrDefault(kvp => string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(prop.Key) && prop.Value is JsonObject childObj)
        {
          return childObj;
        }
      }
      return null;
    }

    private static string NormalizeCity(string? s)
    {
      if (string.IsNullOrWhiteSpace(s)) return string.Empty;
      var str = s.Trim();
      var idx = str.IndexOf('(');
      if (idx >= 0) str = str[..idx];
      str = str
        .Replace("\u200C", string.Empty)
        .Replace("\u200F", string.Empty)
        .Replace("\u200E", string.Empty);
      str = str.Replace('\u064A', '\u06CC').Replace('\u0643', '\u06A9');
      str = str.Replace('\u0629', '\u0647');
      return str.Replace("  ", " ").ToLowerInvariant();
    }

    public async Task<Dictionary<string, int>> GetCityNameIdMapAsync()
    {
      var dirs = await GetAvaiableOTADirectionsAsync();
      var map = new Dictionary<string, int>();
      foreach (var d in dirs)
      {
        if (!string.IsNullOrWhiteSpace(d.Cityone) && d.CityoneId.HasValue)
        {
          var key = NormalizeCity(d.Cityone);
          if (!map.ContainsKey(key)) map[key] = d.CityoneId.Value;
        }
        if (!string.IsNullOrWhiteSpace(d.Citytwo) && d.CitytwoId.HasValue)
        {
          var key = NormalizeCity(d.Citytwo);
          if (!map.ContainsKey(key)) map[key] = d.CitytwoId.Value;
        }
      }
      return map;
    }

    public async Task<bool> ChargeOTABalanceAsync(int amount)
    {
      try
      {
        var content = new StringContent($"charge_amount={amount}", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await _client.PostAsync("/OTAManagement/ChargeOTA", content);
        return response.IsSuccessStatusCode;
      }
      catch
      {
        return false;
      }
    }
  }
}
