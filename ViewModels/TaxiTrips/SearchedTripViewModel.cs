using System;

namespace Application.ViewModels.TaxiTrips
{
  public class SearchedTripViewModel
  {
    public string tripcode { get; set; }
    public string origin { get; set; }
    public string destination { get; set; }
    public string startingDateTime { get; set; }
    public string arrivalDateTime { get; set; }
    public string taxiSupervisorName { get; set; }
    public int taxiSupervisorID { get; set; }
    /// <summary>ORS original list price (before ORS discount).</summary>
    public string originalPrice { get; set; }
    /// <summary>Recommended passenger sale price (ORS after-discount list).</summary>
    public string afterdiscount { get; set; }
    /// <summary>Amount the agency pays after commission reduction.</summary>
    public string payablePrice { get; set; }
    public int commissionPercent { get; set; }
    public bool hasCommission { get; set; }
    public string carModelName { get; set; }
    public string Image { get; set; }
  }
}
