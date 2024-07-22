using System;

public class Ticket
{
  public int Id { get; set; }
  public string Tripcode { get; set; }
  public string TicketCode { get; set; }
  public string Firstname { get; set; }
  public string Lastname { get; set; }
  public string PhoneNumber { get; set; }
  public string Email { get; set; }
  public string NaCode { get; set; }
  public DateTime DOB { get; set; }
  public int TicketOriginalPrice { get; set; }
  public int TicketFinalPrice { get; set; }
  public string TripOrigin { get; set; }
  public string TripDestination { get; set; }
  public DateTime RegisteredAt { get; set; }
  public bool IsCancelled { get; set; }
}
