namespace Application.Services.MrShooferORS
{
  public class DirectionsRepository
  {
    private Dictionary<string, int> DirectionsDictionary = new Dictionary<string, int>
    {
      { "تهران" ,360},
      { "شیراز",758 },
      {"رشت", 1031 },
      {"اصفهان",170 }
    };



    public Dictionary<string,int> GetDirections()
    {
      return DirectionsDictionary; 
    }
  }
}
