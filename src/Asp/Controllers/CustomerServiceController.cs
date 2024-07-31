using Application.Data;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Application.Controllers
{
  public class CustomerServiceController : Controller
  {
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;
    private Agency agency;


    public CustomerServiceController(MrShooferAPIClient apiclient, AppDbContext context)
    {
      this.context = context;
      this.apiclient = apiclient;
    }





    [Route("ReserveInfo")]
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


    public async Task<IActionResult> TripReceiptPDF(string reference)
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

    private void SetAPIClientToken(string agancytoken)
    {
      apiclient.SetSellerApiKey(agancytoken);
    }
  }
}
