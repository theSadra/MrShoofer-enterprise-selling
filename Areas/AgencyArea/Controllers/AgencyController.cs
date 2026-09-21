using Application.Data;
using Application.Services;
using Application.Services.MrShooferORS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.ViewModels;
using Application.Models;
namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [Authorize]
  public class AgencyController : Controller
  {
    private readonly UserManager<IdentityUser> _userManager;
    private readonly MrShooferAPIClient _apiClient;
    private readonly AppDbContext _context;
    private Agency agency;

    public AgencyController(AppDbContext context, UserManager<IdentityUser> userManager, MrShooferAPIClient apiClient)
    {
      _context = context;
      _userManager = userManager;
      _apiClient = apiClient;
    }

    // Agency / organization profile
    [HttpGet]
    public async Task<IActionResult> Index()
    {
      ViewBag.agency = agency;

      var balanceStr = await _apiClient.GetAccountBalance();
      ViewBag.agancy_balance = balanceStr != null ? (long)Convert.ToDecimal(balanceStr) : 0L;

      return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Agency")]
    [Route("/Agency/Index")]
    [Route("/Agency/UpdateProfile")]
    public async Task<IActionResult> UpdateProfile(Application.ViewModels.Agency.AgencyProfileEditViewModel model)
    {
      if (agency == null)
        return RedirectToAction("Index", "Home");

      if (!ModelState.IsValid)
      {
        TempData["ErrorMessage"] = "لطفاً اطلاعات را کامل و صحیح وارد کنید.";
        return RedirectToAction(nameof(Index));
      }

      var name = model.Name.Trim();
      var adminMobile = model.AdminMobile.Trim();
      var phone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
      var address = model.Address.Trim();

      // Local panel fields (incl. financial) must save even when ORS sync is unavailable.
      agency.Name = name;
      agency.AdminMobile = adminMobile;
      agency.PhoneNumber = phone;
      agency.Address = address;
      if (agency.IsOrganization)
      {
        agency.EconomicNo = string.IsNullOrWhiteSpace(model.EconomicNo) ? null : model.EconomicNo.Trim();
        agency.RegistrationNo = string.IsNullOrWhiteSpace(model.RegistrationNo) ? null : model.RegistrationNo.Trim();
        agency.NationalId = string.IsNullOrWhiteSpace(model.NationalId) ? null : model.NationalId.Trim();
        agency.Fax = string.IsNullOrWhiteSpace(model.Fax) ? null : model.Fax.Trim();
        agency.Province = string.IsNullOrWhiteSpace(model.Province) ? null : model.Province.Trim();
        agency.County = string.IsNullOrWhiteSpace(model.County) ? null : model.County.Trim();
        agency.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
        agency.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim();
      }
      await _context.SaveChangesAsync();

      // Best-effort ORS sync for identity fields — never surface sync failures to the user.
      _ = await _apiClient.UpdateMyAgencyInfoAsync(
        companyName: name,
        numberPhone: adminMobile,
        backupNumberPhone: phone,
        companyAddress: address);

      var pendingTicket = TempData["PendingInvoiceTicketCode"] as string;
      if (agency.IsOrganization && !agency.HasInvoiceFinancialInfo)
      {
        if (!string.IsNullOrWhiteSpace(pendingTicket))
          TempData["PendingInvoiceTicketCode"] = pendingTicket;
        TempData["InvoiceBlocked"] = true;
        TempData["ErrorMessage"] = "اطلاعات آژانس ذخیره شد؛ برای صدور فاکتور رسمی، کد اقتصادی، شماره ثبت و شناسه ملی را کامل کنید.";
        return RedirectToAction(nameof(Index));
      }

      TempData["SuccessMessage"] = "اطلاعات پروفایل با موفقیت ذخیره شد.";

      if (agency.IsOrganization && !string.IsNullOrWhiteSpace(pendingTicket) && agency.HasInvoiceFinancialInfo)
        return RedirectToAction("Create", "OfficialInvoices", new { area = "AgencyArea", ticketcode = pendingTicket });

      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult LegalProfile()
    {
      if (agency == null) return RedirectToAction("Index", "Home");
      // Profile page hosts financial info; keep invoice-gate TempData.
      return Redirect(Url.Action(nameof(Index), "Agency", new { area = "AgencyArea" }) + "#financial");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LegalProfile(Application.ViewModels.Agency.AgencyLegalProfileViewModel model)
    {
      if (agency == null) return RedirectToAction("Index", "Home");

      if (!string.IsNullOrWhiteSpace(model.Address))
        agency.Address = model.Address.Trim();

      agency.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
      agency.EconomicNo = string.IsNullOrWhiteSpace(model.EconomicNo) ? null : model.EconomicNo.Trim();
      agency.RegistrationNo = string.IsNullOrWhiteSpace(model.RegistrationNo) ? null : model.RegistrationNo.Trim();
      agency.NationalId = string.IsNullOrWhiteSpace(model.NationalId) ? null : model.NationalId.Trim();
      agency.Fax = string.IsNullOrWhiteSpace(model.Fax) ? null : model.Fax.Trim();
      agency.Province = string.IsNullOrWhiteSpace(model.Province) ? null : model.Province.Trim();
      agency.County = string.IsNullOrWhiteSpace(model.County) ? null : model.County.Trim();
      agency.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
      agency.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim();
      await _context.SaveChangesAsync();

      var pendingTicket = TempData["PendingInvoiceTicketCode"] as string;
      if (agency.IsOrganization && agency.HasInvoiceFinancialInfo)
      {
        TempData["SuccessMessage"] = "اطلاعات مالی / حقوقی ذخیره شد.";
        if (!string.IsNullOrWhiteSpace(pendingTicket))
          return RedirectToAction("Create", "OfficialInvoices", new { area = "AgencyArea", ticketcode = pendingTicket });
        return Redirect(Url.Action(nameof(Index), "Agency", new { area = "AgencyArea" }) + "#financial");
      }

      if (agency.IsOrganization)
      {
        if (!string.IsNullOrWhiteSpace(pendingTicket))
          TempData["PendingInvoiceTicketCode"] = pendingTicket;
        TempData["InvoiceBlocked"] = true;
        TempData["ErrorMessage"] = "برای صدور فاکتور رسمی، کد اقتصادی، شماره ثبت و شناسه ملی را وارد کنید.";
      }
      else
      {
        TempData["SuccessMessage"] = "اطلاعات مالی ذخیره شد.";
      }

      return Redirect(Url.Action(nameof(Index), "Agency", new { area = "AgencyArea" }) + "#financial");
    }


    [HttpGet]
    public JsonResult GetSalesChartValues()
    {
      AgencyAnalyzerService analyzer = new AgencyAnalyzerService(agency);
      _context.Entry(agency)
        .Collection(a => a.SoldTickets)
        .Load();

      var valuesdictionary = analyzer.GetLast7Days_SaleChartNumbers();
      // Your data array
      var newdictionary = valuesdictionary.ToDictionary(
        kv => kv.Key.ToPersianDate().Month + "/" + kv.Key.ToPersianDate().Day,
        kv => kv.Value);




      var last = newdictionary.Last();
      var oldkey = last.Key;
      var value = last.Value;

      newdictionary.Remove(oldkey);
      newdictionary.Add("امروز", value);
      //int[] values = newdictionary.Select(kv => kv.Value).ToArray();
      //string[] daynames = newdictionary.Select(kv => kv.Value.ToString()).ToArray();

      // Return the array as a JSON object
      return Json(newdictionary);
    }


    // For setting api key and getting agency entity related to current request from database
    public override void OnActionExecuting(ActionExecutingContext context)
    {
      base.OnActionExecuting(context);
        
      var identityUser = _userManager.GetUserAsync(User).Result;
      agency = _context.Agencies.FirstOrDefault(a => a.IdentityUser == identityUser);

      if (agency != null && !string.IsNullOrWhiteSpace(agency.ORSAPI_token))
        _apiClient.SetSellerApiKey(agency.ORSAPI_token);
    }
  }
}
