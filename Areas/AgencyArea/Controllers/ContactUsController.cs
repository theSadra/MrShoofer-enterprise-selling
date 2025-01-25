using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Application.Areas.AgencyArea.Controllers
{
  public class ContactUsController : Controller
  {
    private readonly AppDbContext _context;

    private readonly IConfiguration configuration;

    public ContactUsController(AppDbContext context, IConfiguration configuration)
    {
      this._context = context;
      this.configuration = configuration;
    }


    [HttpPost("/Message")]
    [EnableRateLimiting("ContactUsPolicy")]
    public async Task<IActionResult> PostContactMessage([FromBody] ContactUsMessage msg)
    {
      msg.RegisteredDateTime = DateTime.Now;
      _context.Add(msg);
      await _context.SaveChangesAsync();

      return Ok();
    }
  }
}
