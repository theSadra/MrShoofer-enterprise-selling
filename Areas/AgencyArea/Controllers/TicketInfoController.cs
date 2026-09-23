using Application.Data;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Application.Models;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize]
  public class TicketInfoController : Controller
  {
    private const int PageSize = 20;

    private readonly AppDbContext context;
    private readonly UserManager<IdentityUser> userManager;
    private readonly ITicketStatusSyncService _ticketStatusSync;

    public TicketInfoController(
      AppDbContext context,
      UserManager<IdentityUser> userManager,
      ITicketStatusSyncService ticketStatusSync)
    {
      this.context = context;
      this.userManager = userManager;
      _ticketStatusSync = ticketStatusSync;
    }

    public async Task<IActionResult> Index(int page = 1, string? datesFilter = null)
    {
      if (page < 1) page = 1;

      var username = User?.Identity != null ? User.Identity.Name : null;
      var agency = await context.Agencies
        .Include(a => a.IdentityUser)
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.IdentityUser.UserName == username);

      if (agency == null)
      {
        BindEmpty(page);
        return View();
      }

      // Refresh cancellation flags from ORS so cancelled tickets leave upcoming / active lists.
      try
      {
        await _ticketStatusSync.SyncRecentAsync(agency.Id, agency.ORSAPI_token, maxTickets: 20);
      }
      catch
      {
      }

      var query = context.Tickets
        .AsNoTracking()
        .Where(t => EF.Property<int>(t, "AgencyId") == agency.Id);

      if (!string.IsNullOrWhiteSpace(datesFilter))
      {
        var range = TryParseDateFilter(datesFilter);
        if (range == null)
          return RedirectToAction(nameof(Index), new { page });

        query = query.Where(t => t.RegisteredAt >= range.Value.Start && t.RegisteredAt <= range.Value.End);
        ViewBag.dateFilter = datesFilter;
      }

      var totalCount = await query.CountAsync();
      var activeCount = await query.CountAsync(t => !t.IsCancelled);
      var cancelledCount = totalCount - activeCount;
      var totalPaid = await query.Where(t => !t.IsCancelled).SumAsync(t => (long)t.TicketFinalPrice);

      var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
      if (page > totalPages) page = totalPages;

      var tickets = await query
        .OrderByDescending(t => t.RegisteredAt)
        .Skip((page - 1) * PageSize)
        .Take(PageSize)
        .ToListAsync();

      ViewBag.tickets = tickets;
      ViewBag.page = page;
      ViewBag.pageSize = PageSize;
      ViewBag.totalCount = totalCount;
      ViewBag.totalPages = totalPages;
      ViewBag.activeCount = activeCount;
      ViewBag.cancelledCount = cancelledCount;
      ViewBag.totalPaid = totalPaid;
      ViewBag.isOrganization = agency.IsOrganization;
      ViewBag.hasInvoiceFinancialInfo = agency.HasInvoiceFinancialInfo;
      ViewBag.legalProfileUrl = Url.Action("Index", "Agency", new { area = "AgencyArea" }) + "#financial";

      return View();
    }

    [HttpGet]
    public IActionResult Filter(string datesFilter, int page = 1)
    {
      return RedirectToAction(nameof(Index), new { datesFilter, page });
    }

    private static (DateTime Start, DateTime End)? TryParseDateFilter(string datesFilter)
    {
      var dateStrings = datesFilter.Replace(" ", "").Split('-');
      if (dateStrings.Length < 2) return null;

      try
      {
        var startDate = new PersianDate(dateStrings[0]).ToDateTime();
        startDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);

        var endDate = new PersianDate(dateStrings[1]).ToDateTime();
        endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);

        return (startDate, endDate);
      }
      catch
      {
        return null;
      }
    }

    private void BindEmpty(int page)
    {
      ViewBag.tickets = new List<Ticket>();
      ViewBag.page = page;
      ViewBag.pageSize = PageSize;
      ViewBag.totalCount = 0;
      ViewBag.totalPages = 1;
      ViewBag.activeCount = 0;
      ViewBag.cancelledCount = 0;
      ViewBag.totalPaid = 0L;
    }
  }
}
