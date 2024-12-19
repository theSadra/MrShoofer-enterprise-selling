using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Services.MrShooferORS
{
  public class DirectionsRepository
  {
    // CityIDS dictionary
    private Dictionary<string, int> DirectionsDictionary = new Dictionary<string, int>
    {
      { "تهران" ,360},
      { "شیراز",758 },
      {"رشت", 1031 },
      {"اصفهان",170 },
      {"لاهیجان", 1066 },
      {"چالوس", 1123 },
      {"نوشهر", 1156 },
      {"رامسر", 1126 },
      {"ساری", 1129 },
      {"گرگان", 1015 },
      {"تبریز", 45 },
      {"زنجان", 647 },
      {"کرمانشاه", 967 },
      {"کاشان", 234 },
      {"همدان", 1256 },
      {"قم", 841 },
      {"شهرکرد", 407 },
      {"سنندج", 861 }
    };



    public Dictionary<string, int> GetDirections()
    {
      return DirectionsDictionary;
    }
  }


  public class DirectionsTravelTimeCalculator
  {
    private readonly IWebHostEnvironment _env;



    private JsonDocument document { get; set; }
    private JsonElement documentroot { get; set; }

    public DirectionsTravelTimeCalculator(IWebHostEnvironment env)
    {
      _env = env;
      string jsonFilePath = Path.Combine(_env.WebRootPath, "json", "Directions", "Directions.json");

      if (!System.IO.File.Exists(jsonFilePath))
      {
        throw new Exception("Directions.json file doesnot exists...");
      }

      // Read the JSON file
      string jsonData = System.IO.File.ReadAllText(jsonFilePath);

      document = JsonDocument.Parse(jsonData);
      documentroot = document.RootElement;
    }

    public int GetTravelMins(string originCity, string destinationCity)
    {

      try
      {

        var match = documentroot.EnumerateArray()

              .FirstOrDefault(element => (
                  element.GetProperty("Cityone").GetString() == originCity &&
                  element.GetProperty("Citytwo").GetString() == destinationCity) ||

                  (element.GetProperty("Citytwo").GetString() == originCity &&
                  element.GetProperty("Cityone").GetString() == destinationCity));

        if (match.ValueKind == JsonValueKind.Undefined)
        {
          return 0;
        }

        return match.GetProperty("TravelTime_mins").GetInt32();
      }
      catch (Exception ex)
      {
        return 0;
      }
    }
  }
}
