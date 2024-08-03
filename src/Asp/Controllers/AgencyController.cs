using Application.Data;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Controllers
{
  [Authorize]
  [Route("api/[controller]")]
  [ApiController]
  public class AgenciesController : ControllerBase
  {
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient _apiClient;
    private readonly AppDbContext _context;

    public AgenciesController(AppDbContext context, UserManager<IdentityUser> userManager, MrShooferAPIClient apiClient)
    {
      _context = context;
      _userManager = userManager;
      _apiClient = apiClient;
    }

    // GET: api/Agencies
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Agency>>> GetAgencies()
    {
      return await _context.Agencies.Include(a => a.SoldTickets).ToListAsync();
    }


    // Get purchased trips for an agency
    [HttpGet("{id}/trips")]
    public async Task<ActionResult<IEnumerable<Ticket>>> GetPurchasedTrips(int id)
    {
      var agency = await _context.Agencies.Include(a => a.SoldTickets).FirstOrDefaultAsync(a => a.Id == id);

      if (agency == null)
      {
        return NotFound();
      }

      return agency.SoldTickets;
    }

    // Get the balance of the agency account
    [HttpGet("{id}/balance")]
    public async Task<ActionResult<int>> GetAgencyBalance(int id)
    {
      var agency = await _context.Agencies.FindAsync(id);

      if (agency == null)
      {
        return NotFound();
      }

      var agancyUser = await _userManager.FindByIdAsync(agency.IdentityUser.Id);
      var balance = await _apiClient.GetAccountBalance();

      return (int)Convert.ToDouble(balance);
    }

  }
}
