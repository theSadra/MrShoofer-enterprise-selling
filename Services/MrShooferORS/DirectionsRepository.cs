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



    public Dictionary<string,int> GetDirections()
    {
      return DirectionsDictionary; 
    }
  }
}
