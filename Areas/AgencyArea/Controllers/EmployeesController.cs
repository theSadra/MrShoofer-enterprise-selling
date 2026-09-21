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
    public async Task<IActionResult> Index(string? q = null, int? openCreate = null)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      return await RenderIndexAsync(q, new AgencyEmployeeFormViewModel(), openCreate == 1);
    }

    [HttpGet]
    public IActionResult Create()
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      return RedirectToAction(nameof(Index), new { openCreate = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AgencyEmployeeFormViewModel model)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      if (!ModelState.IsValid)
        return await RenderIndexAsync(null, model, openCreate: true);

      var exists = await _context.AgencyEmployees
        .AnyAsync(e => e.AgencyId == _agency.Id && e.NaCode == model.NaCode);
      if (exists)
      {
        ModelState.AddModelError(nameof(model.NaCode), "فردی با این کد ملی قبلاً ثبت شده است");
        return await RenderIndexAsync(null, model, openCreate: true);
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
        InternalTitle = _agency.IsOrganization && !string.IsNullOrWhiteSpace(model.InternalTitle)
          ? model.InternalTitle.Trim()
          : null,
        CreatedAt = now,
        UpdatedAt = now
      });
      await _context.SaveChangesAsync();
      TempData["SuccessMessage"] = _agency.IsOrganization ? "عضو با موفقیت افزوده شد" : "مسافر با موفقیت افزوده شد";
      return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RenderIndexAsync(
      string? q,
      AgencyEmployeeFormViewModel createModel,
      bool openCreate)
    {
      var query = _context.AgencyEmployees
        .Where(e => e.AgencyId == _agency!.Id);

      if (!string.IsNullOrWhiteSpace(q))
      {
        var term = q.Trim();
        query = query.Where(e =>
          e.Firstname.Contains(term) ||
          e.Lastname.Contains(term) ||
          e.NaCode.Contains(term) ||
          e.PhoneNumber.Contains(term) ||
          (e.Email != null && e.Email.Contains(term)) ||
          (e.InternalTitle != null && e.InternalTitle.Contains(term)));
      }

      var employees = await query
        .OrderBy(e => e.Lastname)
        .ThenBy(e => e.Firstname)
        .ToListAsync();

      var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
      var lookback = monthStart.AddMonths(-6);
      var recentTicketRows = await _context.Tickets
        .AsNoTracking()
        .Where(t => EF.Property<int>(t, "AgencyId") == _agency!.Id
                    && !t.IsCancelled
                    && t.RegisteredAt >= lookback)
        .Select(t => new
        {
          t.AgencyEmployeeId,
          t.NaCode,
          t.PhoneNumber,
          t.Firstname,
          t.Lastname,
          t.RegisteredAt
        })
        .ToListAsync();

      var employeeIdsWithTrips = new HashSet<int>();
      var monthTripCount = 0;
      foreach (var row in recentTicketRows)
      {
        var ticket = new Ticket
        {
          AgencyEmployeeId = row.AgencyEmployeeId,
          NaCode = row.NaCode,
          PhoneNumber = row.PhoneNumber,
          Firstname = row.Firstname,
          Lastname = row.Lastname,
          RegisteredAt = row.RegisteredAt
        };
        var matched = ResolveEmployeeForTicket(ticket, employees);
        if (matched == null) continue;
        employeeIdsWithTrips.Add(matched.Id);
        if (ticket.RegisteredAt >= monthStart)
          monthTripCount++;
      }

      var readyToBookCount = employees.Count(e =>
        !string.IsNullOrWhiteSpace(e.NaCode)
        && e.NaCode.Trim().Length == 10
        && e.NaCode.Trim().All(char.IsDigit)
        && !string.IsNullOrWhiteSpace(e.PhoneNumber)
        && !string.IsNullOrWhiteSpace(e.Gender));

      SetPeopleLabels();
      ViewBag.Search = q ?? "";
      ViewBag.TotalCount = employees.Count;
      ViewBag.WithTripsCount = employeeIdsWithTrips.Count;
      ViewBag.MonthTripCount = monthTripCount;
      ViewBag.ReadyToBookCount = readyToBookCount;
      ViewBag.OpenCreateModal = openCreate;
      ViewBag.CreateModel = createModel;
      return View("Index", employees);
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
    public IActionResult SpendReport(int? year, int? month, string? from, string? to)
    {
      var qs = new List<string>();
      if (year is int y) qs.Add($"year={y}");
      if (month is int m) qs.Add($"month={m}");
      if (!string.IsNullOrWhiteSpace(from)) qs.Add($"from={Uri.EscapeDataString(from)}");
      if (!string.IsNullOrWhiteSpace(to)) qs.Add($"to={Uri.EscapeDataString(to)}");
      var url = "/Analytics" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
      return Redirect(url);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == id && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      SetPeopleLabels();
      ViewBag.PageTitle = _agency.IsOrganization ? "ویرایش عضو" : "ویرایش مسافر";
      return View(new AgencyEmployeeFormViewModel
      {
        Id = employee.Id,
        Firstname = employee.Firstname,
        Lastname = employee.Lastname,
        Gender = employee.Gender,
        NaCode = employee.NaCode,
        PhoneNumber = employee.PhoneNumber,
        Email = employee.Email,
        InternalTitle = employee.InternalTitle
      });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AgencyEmployeeFormViewModel model)
    {
      if (_agency == null) return RedirectToAction("Index", "Home");
      if (!model.Id.HasValue) return BadRequest();
      SetPeopleLabels();
      ViewBag.PageTitle = _agency.IsOrganization ? "ویرایش عضو" : "ویرایش مسافر";
      if (!ModelState.IsValid) return View(model);

      var employee = await _context.AgencyEmployees
        .FirstOrDefaultAsync(e => e.Id == model.Id.Value && e.AgencyId == _agency.Id);
      if (employee == null) return NotFound();

      var duplicate = await _context.AgencyEmployees
        .AnyAsync(e => e.AgencyId == _agency.Id && e.NaCode == model.NaCode && e.Id != employee.Id);
      if (duplicate)
      {
        ModelState.AddModelError(nameof(model.NaCode), "فردی با این کد ملی قبلاً ثبت شده است");
        return View(model);
      }

      employee.Firstname = model.Firstname.Trim();
      employee.Lastname = model.Lastname.Trim();
      employee.Gender = model.Gender;
      employee.NaCode = model.NaCode.Trim();
      employee.PhoneNumber = model.PhoneNumber.Trim();
      employee.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
      employee.InternalTitle = _agency.IsOrganization && !string.IsNullOrWhiteSpace(model.InternalTitle)
        ? model.InternalTitle.Trim()
        : null;
      employee.UpdatedAt = DateTime.Now;

      await _context.SaveChangesAsync();
      TempData["SuccessMessage"] = "اطلاعات با موفقیت به‌روزرسانی شد";
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
          email = e.Email,
          internalTitle = e.InternalTitle
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
        InternalTitle = _agency.IsOrganization && !string.IsNullOrWhiteSpace(model.InternalTitle)
          ? model.InternalTitle.Trim()
          : null,
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
          email = entity.Email,
          internalTitle = entity.InternalTitle
        }
      });
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);
      var identityUser = _userManager.GetUserAsync(User).Result;
      _agency = _context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);
    }

    private void SetPeopleLabels()
    {
      var isOrg = _agency?.IsOrganization == true;
      ViewBag.IsOrganization = isOrg;
      ViewBag.PageTitle = isOrg ? "اعضای سازمان" : "مسافرین";
      ViewBag.PageSubtitle = isOrg
        ? "مدیریت اعضای سازمان، سمت‌ها و نقش‌های درون‌سازمانی"
        : "دفترچه مسافرین برای رزرو سریع‌تر";
      ViewBag.CreateLabel = isOrg ? "افزودن عضو" : "افزودن مسافر";
      ViewBag.EntityLabel = isOrg ? "عضو" : "مسافر";
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
