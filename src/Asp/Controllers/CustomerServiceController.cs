using Application.Data;
using Application.Services.MrShooferORS;
using Application.ViewModels.CustomerService;
using iText.Html2pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Controllers
{
  public class CustomerServiceController : Controller
  {
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;
    private Agency agency;


    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public CustomerServiceController(MrShooferAPIClient apiclient, AppDbContext context, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider)
    {
      this.context = context;
      this.apiclient = apiclient;


      _viewEngine = viewEngine;
      _tempDataProvider = tempDataProvider;
      _serviceProvider = serviceProvider;
    }





    [Route("/ReserveInfo")]
    public async Task<IActionResult> CustomerReserveInfo(string reference)
    {
      var ticket = context.Tickets.Where(t => t.TicketCode == reference).FirstOrDefault();


      // Loading Agancy ticket registered by
      context.Entry(ticket)
        .Reference(t => t.Agency)
        .Load();

      SetAPIClientToken(ticket.Agency.ORSAPI_token);


      var trip = await apiclient.GetTripInfo(ticket.Tripcode);
      ViewBag.trip = trip;
      ViewBag.ticket = ticket;


      return View();
    }



    [Route("/TripReceiptPdf")]
    public async Task<IActionResult> TripReceiptPDF(string reference)
    {
      var ticket = context.Tickets.Where(t => t.TicketCode == reference).FirstOrDefault();


      // Loading Agancy ticket registered by
      context.Entry(ticket)
        .Reference(t => t.Agency)
        .Load();

      SetAPIClientToken(ticket.Agency.ORSAPI_token);

      var trip = await apiclient.GetTripInfo(ticket.Tripcode);

      CustomerReciptPdfGeneratorViewModel viewmodel = new CustomerReciptPdfGeneratorViewModel()
      {
        Trip = trip,
        Ticket = ticket
      };


      return View("TripReciptionPdfView", viewmodel);
      }

    private void SetAPIClientToken(string agancytoken)
    {
      apiclient.SetSellerApiKey(agancytoken);
    }


    private void GeneratePdf(string htmlcontent, FileStream stream)
    {
      HtmlConverter.ConvertToPdf(htmlcontent, stream);

    }
  }
}
