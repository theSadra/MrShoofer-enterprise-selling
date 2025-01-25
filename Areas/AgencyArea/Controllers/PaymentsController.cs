using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.AgencyArea.Controllers
{

  [Area("AgencyArea")]
  [Authorize]
  public class PaymentsController : Controller
  {


    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    public PaymentsController(AppDbContext dbContext, UserManager<IdentityUser> usermanager)
    {
      this._context = dbContext;
      this._userManager = usermanager;
    }

    [HttpPost("/Payments/ChargeRequest")]
    public async Task<IActionResult> ChargePaymentRequest(string amount, string method, string? message)
    {
      var identityUser = await _userManager.FindByNameAsync(User.Identity.Name);

      var agency = await _context.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.IdentityUser == identityUser);


      var chargeRequest = new ChargePaymentRequest()
      {
        Amout = amount.Replace(",", ""),
        PaymentMethod = method,
        Message = message,
        Agency = agency,
        RequestedOn = DateTime.Now
      };


      try
      {
        _context.Attach(agency);
        _context.ChargePaymentRequests.Add(chargeRequest);
        _context.SaveChanges();
        return Ok();
      }

      catch (Exception e)
      {
        return BadRequest();
      }

    }
  }
}
