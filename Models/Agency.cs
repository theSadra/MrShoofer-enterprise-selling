using Microsoft.AspNetCore.Identity;

namespace Application.Models
{
  public class Agency
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string Address { get; set; }
    public string AdminMobile { get; set; }
    public DateTime DateJoined { get; set; }
    public string ORSAPI_token { set; get; }
    public int Commission { get; set; }

    public IdentityUser IdentityUser { get; set; }
    public ICollection<Ticket> SoldTickets { get; set; }
  }

}
