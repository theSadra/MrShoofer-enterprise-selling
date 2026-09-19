using Application.Data;
using Application.Models;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Application.Areas.AgencyArea.Controllers
{
  public class ContactUsController : Controller
  {
    private readonly AppDbContext _context;
    private readonly CustomerServiceSmsSender _sms;
    private readonly ILogger<ContactUsController> _logger;

    public ContactUsController(
      AppDbContext context,
      CustomerServiceSmsSender sms,
      ILogger<ContactUsController> logger)
    {
      _context = context;
      _sms = sms;
      _logger = logger;
    }

    [HttpPost("/Message")]
    [EnableRateLimiting("ContactUsPolicy")]
    public async Task<IActionResult> PostContactMessage([FromBody] ContactUsMessage msg)
    {
      msg.RegisteredDateTime = DateTime.Now;
      _context.Add(msg);
      await _context.SaveChangesAsync();

      try
      {
        await _sms.NotifyAdminNewContactMessageAsync(msg.Name, msg.Number, msg.Message);
      }
      catch (Exception ex)
      {
        // Message is already persisted; do not fail the contact form if SMS fails.
        _logger.LogError(ex, "Failed to send admin SMS for contact message {MessageId}", msg.Id);
      }

      return Ok();
    }
  }
}
