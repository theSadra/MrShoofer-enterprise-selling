using NuGet.Protocol.Plugins;

namespace Application.Models
{
  public class ContactUsMessage
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Number { get; set; }
    public string Message { get; set; }

    public DateTime RegisteredDateTime { get; set; }
  }
}
