using Application.Data;
using Application.Models;
using Application.Services;
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
  public class EmployeesController : Controller
  {
    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private Agency? _agency;

    public EmployeesController(AppDbContext context, UserManager<IdentityUser> userManager)
    {
      _context = context;
      _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var query = _context.AgencyEmployees
        .Where(e => e.AgencyId == _agency.Id);

      if (!string.IsNullOrWhiteSpace(q))
      {
        var term = q.Trim();
        query = query.Where(e =>
          e.Firstname.Contains(term) ||
          e.Lastname.Contains(term) ||
          e.NaCode.Contains(term) ||
          e.PhoneNumber.Contains(term) ||
          (e.Email != null && e.Email.Contains(term)));
      }

      var employees = await query
        .OrderBy(e => e.Lastname)
        .ThenBy(e => e.Firstname)
        .ToListAsync();

      ViewBag.Search = q ?? "";
      return View(employees);
    }

    [HttpGet]
    public IActionResult Create()
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      return View(new AgencyEmployeeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AgencyEmployeeFormViewModel model)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      if (!ModelState.IsValid) return View(model);

      var exists = await _context.AgencyEmployees
        .AnyAsync(e => e.AgencyId == _agency.Id && e.NaCode == model.NaCode);
      if (exists)
      {
        ModelState.AddModelError(nameof(model.NaCode), "مسافری با این کد ملی قبلاً ثبت شده است");
        return View(model);
      }

      var now = DateTime.Now;
      _context.AgencyEmployees.Add(new AgencyEmployee
      {
        AgencyId = _agency.Id,
        Firstname = model.Firstname.Trim(),
        Lastname = model.Lastname.Trim(),
        Gender = model.Gender,
        NaCode = model.NaCode.Trim(),
        PhoneNumber = model.PhoneNumber.Trim(),
        Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
        CreatedAt = now,
        UpdatedAt = now
      });
      await _context.SaveChangesAsync();
      TempData["SuccessMessage"] = "مسافر پرتردد با موفقیت افزوده شد";
      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Trips(int id, string? from = null, string? to = null)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == id && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      var (fromDt, toDt) = ResolveShamsiRange(from, to, defaultMonthSpan: null);

      var tickets = await _context.Tickets
        .AsNoTracking()
        .Include(t => t.Companions)
        .Where(t => EF.Property<int>(t, "AgencyId") == _agency.Id &&
                    !t.IsCancelled &&
                    t.RegisteredAt >= fromDt &&
                    t.RegisteredAt < toDt)
        .ToListAsync();

      tickets = tickets
        .Where(t => TicketMatchesEmployee(t, employee))
        .OrderByDescending(t => t.RegisteredAt)
        .ToList();

      ViewBag.Employee = employee;
      ViewBag.FromShamsi = fromDt.ToPersianDate().ToShortDateString();
      ViewBag.ToShamsi = toDt.AddDays(-1).ToPersianDate().ToShortDateString();
      return View(tickets);
    }

    [HttpGet]
    public async Task<IActionResult> SpendReport(int? year, int? month, string? from, string? to)
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
        (fromDt, toDt) = ResolveShamsiRange(from, to, defaultMonthSpan: null);
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

      var tickets = await _context.Tickets
        .AsNoTracking()
        .Include(t => t.AgencyEmployee)
        .Where(t => EF.Property<int>(t, "AgencyId") == _agency.Id &&
                    t.RegisteredAt >= fromDt &&
                    t.RegisteredAt < toDt &&
                    !t.IsCancelled)
        .ToListAsync();

      var rows = tickets
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

      ViewBag.Year = py;
      ViewBag.Month = pm;
      ViewBag.From = fromDt;
      ViewBag.To = toDt;
      ViewBag.FromShamsi = fromDt.ToPersianDate().ToShortDateString();
      ViewBag.ToShamsi = toDt.AddDays(-1).ToPersianDate().ToShortDateString();
      ViewBag.FilterLabel = filterLabel;
      ViewBag.GrandTotal = rows.Sum(r => r.TotalToman);
      ViewBag.GrandTrips = rows.Sum(r => r.TripCount);
      ViewBag.PersianYears = Enumerable.Range(nowP.Year - 5, 7).Reverse().ToList();
      return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == id && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      return View(new AgencyEmployeeFormViewModel
      {
        Id = employee.Id,
        Firstname = employee.Firstname,
        Lastname = employee.Lastname,
        Gender = employee.Gender,
        NaCode = employee.NaCode,
        PhoneNumber = employee.PhoneNumber,
        Email = employee.Email
      });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AgencyEmployeeFormViewModel model)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      if (!model.Id.HasValue) return BadRequest();
      if (!ModelState.IsValid) return View(model);

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == model.Id.Value && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      var duplicate = await _context.AgencyEmployees
        .AnyAsync(e => e.AgencyId == _agency.Id && e.NaCode == model.NaCode && e.Id != employee.Id);
      if (duplicate)
      {
        ModelState.AddModelError(nameof(model.NaCode), "مسافری با این کد ملی قبلاً ثبت شده است");
        return View(model);
      }

      employee.Firstname = model.Firstname.Trim();
      employee.Lastname = model.Lastname.Trim();
      employee.Gender = model.Gender;
      employee.NaCode = model.NaCode.Trim();
      employee.PhoneNumber = model.PhoneNumber.Trim();
      employee.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
      employee.UpdatedAt = DateTime.Now;

      await _context.SaveChangesAsync();
      TempData["SuccessMessage"] = "اطلاعات مسافر به‌روزرسانی شد";
      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == id && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      _context.AgencyEmployees.Remove(employee);
      await _context.SaveChangesAsync();
      TempData["SuccessMessage"] = "مسافر حذف شد";
      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ListJson()
    {
      if (_agency == null) return Json(Array.Empty<object>());

      var list = await _context.AgencyEmployees
        .Where(e => e.AgencyId == _agency.Id)
        .OrderBy(e => e.Lastname)
        .ThenBy(e => e.Firstname)
        .Select(e => new
        {
          id = e.Id,
          firstname = e.Firstname,
          lastname = e.Lastname,
          gender = e.Gender,
          naCode = e.NaCode,
          phoneNumber = e.PhoneNumber,
          email = e.Email
        })
        .ToListAsync();

      return Json(list);
    }

    /// <summary>Quick-save passenger from the reserve form (JSON).</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QuickCreate([FromBody] AgencyEmployeeFormViewModel model)
    {
      if (_agency == null)
        return Json(new { success = false, message = "آژانس یافت نشد" });

      if (string.IsNullOrWhiteSpace(model.Firstname) ||
          string.IsNullOrWhiteSpace(model.Lastname) ||
          string.IsNullOrWhiteSpace(model.Gender) ||
          string.IsNullOrWhiteSpace(model.NaCode) ||
          string.IsNullOrWhiteSpace(model.PhoneNumber))
      {
        return Json(new { success = false, message = "نام، نام‌خانوادگی، جنسیت، کد ملی و موبایل الزامی است" });
      }

      if (!System.Text.RegularExpressions.Regex.IsMatch(model.NaCode.Trim(), @"^\d{10}$"))
        return Json(new { success = false, message = "کد ملی باید دقیقاً ۱۰ رقم باشد" });

      if (!System.Text.RegularExpressions.Regex.IsMatch(model.PhoneNumber.Trim(), @"^((0?9)|(\+?989))\d{9}$"))
        return Json(new { success = false, message = "شماره موبایل معتبر نیست" });

      var naCode = model.NaCode.Trim();
      var exists = await _context.AgencyEmployees
        .AnyAsync(e => e.AgencyId == _agency.Id && e.NaCode == naCode);
      if (exists)
        return Json(new { success = false, message = "مسافری با این کد ملی قبلاً ثبت شده است" });

      var now = DateTime.Now;
      var entity = new AgencyEmployee
      {
        AgencyId = _agency.Id,
        Firstname = model.Firstname.Trim(),
        Lastname = model.Lastname.Trim(),
        Gender = model.Gender.Trim(),
        NaCode = naCode,
        PhoneNumber = model.PhoneNumber.Trim(),
        Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
        CreatedAt = now,
        UpdatedAt = now
      };
      _context.AgencyEmployees.Add(entity);
      await _context.SaveChangesAsync();

      return Json(new
      {
        success = true,
        message = "مسافر به لیست مسافرین پرتردد افزوده شد",
        passenger = new
        {
          id = entity.Id,
          firstname = entity.Firstname,
          lastname = entity.Lastname,
          gender = entity.Gender,
          naCode = entity.NaCode,
          phoneNumber = entity.PhoneNumber,
          email = entity.Email
        }
      });
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
        var byName = employees.FirstOrDefault(e =>
          string.Equals(e.Firstname.Trim(), first, StringComparison.OrdinalIgnoreCase) &&
          string.Equals(e.Lastname.Trim(), last, StringComparison.OrdinalIgnoreCase));
        if (byName != null) return byName;
      }

      return null;
    }

    private static bool TicketMatchesEmployee(Ticket ticket, AgencyEmployee employee)
    {
      if (ticket.AgencyEmployeeId == employee.Id) return true;

      var na = NormalizeDigits(ticket.NaCode);
      if (!string.IsNullOrEmpty(na) && NormalizeDigits(employee.NaCode) == na) return true;

      var phone = NormalizePhone(ticket.PhoneNumber);
      if (!string.IsNullOrEmpty(phone) && NormalizePhone(employee.PhoneNumber) == phone) return true;

      return string.Equals(ticket.Firstname?.Trim(), employee.Firstname.Trim(), StringComparison.OrdinalIgnoreCase) &&
             string.Equals(ticket.Lastname?.Trim(), employee.Lastname.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDigits(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return "";
      var chars = value.Trim().Where(char.IsDigit).ToArray();
      return new string(chars);
    }

    private static string NormalizePhone(string? value)
    {
      var digits = NormalizeDigits(value);
      if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits.Substring(2);
      if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
      return digits;
    }

    /// <summary>
    /// Parses optional Shamsi yyyy/MM/dd strings. When both empty, defaults to last 365 days
    /// unless used from SpendReport month path.
    /// </summary>
    private static (DateTime from, DateTime toExclusive) ResolveShamsiRange(string? from, string? to, int? defaultMonthSpan)
    {
      var now = DateTime.Now;
      DateTime fromDt;
      DateTime toExclusive;

      if (!string.IsNullOrWhiteSpace(from) && TryParseShamsiDate(from, out var parsedFrom))
        fromDt = parsedFrom.Date;
      else if (defaultMonthSpan == null)
        fromDt = now.Date.AddYears(-1);
      else
      {
        var p = now.ToPersianDate();
        var pc = new PersianCalendar();
        fromDt = pc.ToDateTime(p.Year, p.Month, 1, 0, 0, 0, 0);
      }

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
        var pc = new PersianCalendar();
        result = pc.ToDateTime(y, m, d, 0, 0, 0, 0);
        return true;
      }
      catch
      {
        return false;
      }
    }
  }
}
