using System;
using System.Collections.Generic;

public class Agency
{
  public int Id { get; set; }
  public string Name { get; set; }
  public string PhoneNumber { get; set; }
  public string Address { get; set; }
  public string AdminMobile { get; set; }
  public DateTime DateJoined { get; set; }
  public List<Ticket> SoldTickets { get; set; } = new List<Ticket>();
  public int Balance { get; set; }
}
