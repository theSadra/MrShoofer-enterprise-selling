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
        static string Norm(string? s) =>
          (s ?? "").Replace("‌", "").Replace(" ", "").Trim();

        var o = Norm(originCity);
        var d = Norm(destinationCity);
        if (o.Length == 0 || d.Length == 0)
        {
          return 0;
        }

        JsonElement? exact = null;
        JsonElement? fuzzy = null;
        foreach (var element in documentroot.EnumerateArray())
        {
          var c1 = Norm(element.GetProperty("Cityone").GetString());
          var c2 = Norm(element.GetProperty("Citytwo").GetString());
          if ((c1 == o && c2 == d) || (c2 == o && c1 == d))
          {
            exact = element;
            break;
          }

          if (fuzzy == null)
          {
            var forward = (o.Contains(c1) || c1.Contains(o)) && (d.Contains(c2) || c2.Contains(d));
            var reverse = (o.Contains(c2) || c2.Contains(o)) && (d.Contains(c1) || c1.Contains(d));
            if (forward || reverse)
            {
              fuzzy = element;
            }
          }
        }

        var match = exact ?? fuzzy;
        if (match == null)
        {
          return 0;
        }

        return match.Value.GetProperty("TravelTime_mins").GetInt32();
      }
      catch (Exception ex)
      {
        return 0;
      }
    }
  }
}
