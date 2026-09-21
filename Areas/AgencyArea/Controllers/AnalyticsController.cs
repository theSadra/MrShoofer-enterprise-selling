using Application.Data;
using Application.Models;
using Application.Services;
using Application.ViewModels.Analytics;
using Application.ViewModels.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize]
  [Route("Analytics")]
  public class AnalyticsController : Controller
  {
    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private Agency? _agency;

    public AnalyticsController(AppDbContext context, UserManager<IdentityUser> userManager)
    {
      _context = context;
      _userManager = userManager;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(int? year, int? month, string? from, string? to)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var nowP = DateTime.Now.ToPersianDate();
      int py = year ?? nowP.Year;
      int pm = month ?? nowP.Month;
      if (pm < 1 || pm > 12) pm = nowP.Month;

      DateTime fromDt;
      DateTime toDt;
      string filterLabel;

      if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
      {
        (fromDt, toDt) = ResolveShamsiRange(from, to);
        filterLabel = $"{fromDt.ToPersianDate().ToShortDateString()} تا {toDt.AddDays(-1).ToPersianDate().ToShortDateString()}";
        py = fromDt.ToPersianDate().Year;
        pm = fromDt.ToPersianDate().Month;
      }
      else
      {
        var pc = new PersianCalendar();
        fromDt = pc.ToDateTime(py, pm, 1, 0, 0, 0, 0);
        var days = pc.GetDaysInMonth(py, pm);
        toDt = pc.ToDateTime(py, pm, days, 0, 0, 0, 0).AddDays(1);
        filterLabel = $"{new PersianDate(fromDt).MonthName} {py}";
      }

      var employees = await _context.AgencyEmployees
        .AsNoTracking()
        .Where(e => e.AgencyId == _agency.Id)
        .ToListAsync();

      var allTickets = await _context.Tickets
        .AsNoTracking()
        .Include(t => t.AgencyEmployee)
        .Where(t => EF.Property<int>(t, "AgencyId") == _agency.Id &&
                    t.RegisteredAt >= fromDt &&
                    t.RegisteredAt < toDt)
        .ToListAsync();

      var activeTickets = allTickets.Where(t => !t.IsCancelled).ToList();
      var cancelledCount = allTickets.Count(t => t.IsCancelled);

      var spendRows = activeTickets
        .GroupBy(t => ResolveEmployeeForTicket(t, employees)?.Id)
        .Select(g =>
        {
          var emp = employees.FirstOrDefault(e => e.Id == g.Key)
                    ?? g.Select(t => t.AgencyEmployee).FirstOrDefault(e => e != null);
          var name = emp != null
            ? $"{emp.Firstname} {emp.Lastname}"
            : "بدون تخصیص به مسافر پرتردد";
          return new EmployeeSpendRowViewModel
          {
            AgencyEmployeeId = g.Key,
            DisplayName = name,
            TripCount = g.Count(),
            TotalToman = g.Sum(t => t.TicketFinalPrice)
          };
        })
        .OrderByDescending(r => r.TotalToman)
        .ToList();

      var grandTrips = spendRows.Sum(r => r.TripCount);
      var grandTotal = spendRows.Sum(r => r.TotalToman);
      var passengerCount = spendRows.Count(r => r.AgencyEmployeeId != null);
      var avgTicket = grandTrips > 0 ? (int)Math.Round(grandTotal / (double)grandTrips) : 0;

      var topRoutes = activeTickets
        .GroupBy(t => new
        {
          Origin = string.IsNullOrWhiteSpace(t.TripOrigin) ? "—" : t.TripOrigin.Trim(),
          Destination = string.IsNullOrWhiteSpace(t.TripDestination) ? "—" : t.TripDestination.Trim()
        })
        .Select(g => new AnalyticsRouteRowViewModel
        {
          Origin = g.Key.Origin,
          Destination = g.Key.Destination,
          TripCount = g.Count(),
          TotalToman = g.Sum(t => t.TicketFinalPrice)
        })
        .OrderByDescending(r => r.TripCount)
        .ThenByDescending(r => r.TotalToman)
        .Take(8)
        .ToList();

      var dailyLookup = activeTickets
        .GroupBy(t => t.RegisteredAt.Date)
        .ToDictionary(
          g => g.Key,
          g => new { Trips = g.Count(), Spend = g.Sum(t => t.TicketFinalPrice) });

      var dailySeries = new List<AnalyticsDailyPointViewModel>();
      for (var day = fromDt.Date; day < toDt.Date; day = day.AddDays(1))
      {
        dailyLookup.TryGetValue(day, out var point);
        var p = day.ToPersianDate();
        dailySeries.Add(new AnalyticsDailyPointViewModel
        {
          IsoDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
          Label = $"{p.Month:00}/{p.Day:00}",
          Trips = point?.Trips ?? 0,
          SpendToman = point?.Spend ?? 0
        });
      }

      // Cap chart points for very long ranges (keep last 60 days of series)
      if (dailySeries.Count > 60)
        dailySeries = dailySeries.Skip(dailySeries.Count - 60).ToList();

      var vm = new AnalyticsDashboardViewModel
      {
        FilterLabel = filterLabel,
        Year = py,
        Month = pm,
        FromShamsi = fromDt.ToPersianDate().ToShortDateString(),
        ToShamsi = toDt.AddDays(-1).ToPersianDate().ToShortDateString(),
        PersianYears = Enumerable.Range(nowP.Year - 5, 7).Reverse().ToList(),
        GrandTrips = grandTrips,
        GrandTotal = grandTotal,
        PassengerCount = passengerCount,
        AvgTicket = avgTicket,
        CancelledCount = cancelledCount,
        ActiveTickets = activeTickets.Count,
        DailySeries = dailySeries,
        TopRoutes = topRoutes,
        SpendRows = spendRows
      };

      return View(vm);
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);
      var identityUser = _userManager.GetUserAsync(User).Result;
      _agency = _context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);
    }

    private static AgencyEmployee? ResolveEmployeeForTicket(Ticket ticket, List<AgencyEmployee> employees)
    {
      if (ticket.AgencyEmployeeId is int linkedId)
      {
        var byId = employees.FirstOrDefault(e => e.Id == linkedId);
        if (byId != null) return byId;
      }

      var na = NormalizeDigits(ticket.NaCode);
      if (!string.IsNullOrEmpty(na))
      {
        var byNa = employees.FirstOrDefault(e => NormalizeDigits(e.NaCode) == na);
        if (byNa != null) return byNa;
      }

      var phone = NormalizePhone(ticket.PhoneNumber);
      if (!string.IsNullOrEmpty(phone))
      {
        var byPhone = employees.FirstOrDefault(e => NormalizePhone(e.PhoneNumber) == phone);
        if (byPhone != null) return byPhone;
      }

      var first = (ticket.Firstname ?? "").Trim();
      var last = (ticket.Lastname ?? "").Trim();
      if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last))
      {
        return employees.FirstOrDefault(e =>
          string.Equals(e.Firstname.Trim(), first, StringComparison.OrdinalIgnoreCase) &&
          string.Equals(e.Lastname.Trim(), last, StringComparison.OrdinalIgnoreCase));
      }

      return null;
    }

    private static string NormalizeDigits(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return "";
      return new string(value.Trim().Where(char.IsDigit).ToArray());
    }

    private static string NormalizePhone(string? value)
    {
      var digits = NormalizeDigits(value);
      if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits.Substring(2);
      if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
      return digits;
    }

    private static (DateTime from, DateTime toExclusive) ResolveShamsiRange(string? from, string? to)
    {
      var now = DateTime.Now;
      DateTime fromDt;
      DateTime toExclusive;

      if (!string.IsNullOrWhiteSpace(from) && TryParseShamsiDate(from, out var parsedFrom))
        fromDt = parsedFrom.Date;
      else
        fromDt = now.Date.AddYears(-1);

      if (!string.IsNullOrWhiteSpace(to) && TryParseShamsiDate(to, out var parsedTo))
        toExclusive = parsedTo.Date.AddDays(1);
      else
        toExclusive = now.Date.AddDays(1);

      if (toExclusive <= fromDt)
        toExclusive = fromDt.AddDays(1);

      return (fromDt, toExclusive);
    }

    private static bool TryParseShamsiDate(string input, out DateTime result)
    {
      result = default;
      try
      {
        var cleaned = input.Trim().Replace('-', '/');
        var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var y = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var m = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var d = int.Parse(parts[2], CultureInfo.InvariantCulture);
        result = new PersianCalendar().ToDateTime(y, m, d, 0, 0, 0, 0);
        return true;
      }
      catch
      {
        return false;
      }
    }
  }
}
