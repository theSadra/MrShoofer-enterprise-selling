namespace Application.ViewModels.Agency
{
  public class AgencyLegalProfileViewModel
  {
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string AdminMobile { get; set; } = "";

    public string? EconomicNo { get; set; }
    public string? RegistrationNo { get; set; }
    public string? NationalId { get; set; }
    public string? Fax { get; set; }
    public string? Province { get; set; }
    public string? County { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
  }
}
