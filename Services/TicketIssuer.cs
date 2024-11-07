using Application.Data;
using Application.Services.MrShooferORS;
using Application.Models;

namespace Application.Services
{
  public class TicketIssuer
  {
    private readonly MrShooferAPIClient apiclient;
    private readonly AppDbContext context;

    public async Task IssueTicket(Ticket ticket, Agency agency)
    {

    }
  }
}
