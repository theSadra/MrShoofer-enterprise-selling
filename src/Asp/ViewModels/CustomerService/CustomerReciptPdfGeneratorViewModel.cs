using Application.Services.MrShooferORS;

namespace Application.ViewModels.CustomerService
{
  public class CustomerReciptPdfGeneratorViewModel
  {
    public Ticket Ticket { get; set; }
    public SearchedTrip Trip { get; set; }
  }
}
