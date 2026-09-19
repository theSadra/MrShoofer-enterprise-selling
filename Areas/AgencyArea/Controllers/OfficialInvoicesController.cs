using Application.Areas.AgencyArea.ViewModels.OfficialInvoices;
using Application.Data;
using Application.Models;
using Application.Services;
using Application.Services.OfficialInvoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.AgencyArea.Controllers
{
    [Area("AgencyArea")]
    [Authorize]
    public class OfficialInvoicesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IOfficialInvoiceService _invoices;

        public OfficialInvoicesController(AppDbContext db, IOfficialInvoiceService invoices)
        {
            _db = db;
            _invoices = invoices;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var agency = await ResolveAgencyAsync(cancellationToken);
            if (agency == null) return RedirectToAction("Login", "Auth");
            if (!agency.IsOrganization)
            {
                TempData["Error"] = "فاکتور رسمی فقط برای پنل سازمانی فعال است.";
                return RedirectToAction("Index", "Agency");
            }

            ViewData["Title"] = "فاکتور رسمی";
            var list = await _db.OfficialInvoices.AsNoTracking()
                .Where(i => i.AgencyId == agency.Id)
                .OrderByDescending(i => i.Id)
                .Take(200)
                .ToListAsync(cancellationToken);
            ViewBag.SerialPreview = _invoices.GetSerialSettings().Preview;
            ViewBag.HasInvoiceFinancialInfo = agency.HasInvoiceFinancialInfo;
            ViewBag.LegalProfileUrl = Url.Action("LegalProfile", "Agency", new { area = "AgencyArea" });
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string? ticketcode, CancellationToken cancellationToken = default)
        {
            var agency = await ResolveAgencyAsync(cancellationToken);
            if (agency == null) return RedirectToAction("Login", "Auth");
            if (!agency.IsOrganization)
            {
                TempData["Error"] = "فاکتور رسمی فقط برای پنل سازمانی فعال است.";
                return RedirectToAction("Index", "TicketInfo");
            }

            if (!agency.HasInvoiceFinancialInfo)
            {
                TempData["InvoiceBlocked"] = true;
                TempData["ErrorMessage"] = "برای صدور فاکتور رسمی ابتدا اطلاعات مالی آژانس (کد اقتصادی، شماره ثبت و شناسه ملی) را تکمیل کنید.";
                if (!string.IsNullOrWhiteSpace(ticketcode))
                    TempData["PendingInvoiceTicketCode"] = ticketcode.Trim();
                return RedirectToAction("LegalProfile", "Agency");
            }

            if (string.IsNullOrWhiteSpace(ticketcode))
            {
                TempData["Error"] = "فاکتور رسمی فقط از روی بلیط خریداری‌شده و لغوشده‌نشده صادر می‌شود. از فهرست بلیط‌ها اقدام کنید.";
                return RedirectToAction(nameof(Index));
            }

            var (status, ticket, existing) = await _invoices.ResolveEligibleTicketAsync(ticketcode, agency.Id, cancellationToken);
            if (status == TicketInvoiceEligibility.AlreadyInvoiced && existing != null)
            {
                TempData["Success"] = "برای این بلیط قبلاً فاکتور صادر شده است.";
                return RedirectToAction(nameof(Print), new { id = existing.Id });
            }
            if (status != TicketInvoiceEligibility.Ok || ticket == null)
            {
                TempData["Error"] = EligibilityMessage(status);
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = "صدور فاکتور رسمی";
            ViewData["SerialPreview"] = _invoices.GetSerialSettings().Preview;

            var draft = new OfficialInvoice { AgencyId = agency.Id };
            await _invoices.PrefillFromTicketAsync(draft, ticket, agency.Id);
            return View(MapInvoiceToForm(draft));
        }

        [HttpPost]
        [ActionName(nameof(Create))]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConfirm([FromForm] string ticketCode, CancellationToken cancellationToken = default)
        {
            var agency = await ResolveAgencyAsync(cancellationToken);
            if (agency == null) return RedirectToAction("Login", "Auth");
            if (!agency.IsOrganization)
            {
                TempData["Error"] = "فاکتور رسمی فقط برای پنل سازمانی فعال است.";
                return RedirectToAction("Index", "TicketInfo");
            }

            if (!agency.HasInvoiceFinancialInfo)
            {
                TempData["InvoiceBlocked"] = true;
                TempData["ErrorMessage"] = "برای صدور فاکتور رسمی ابتدا اطلاعات مالی آژانس (کد اقتصادی، شماره ثبت و شناسه ملی) را تکمیل کنید.";
                if (!string.IsNullOrWhiteSpace(ticketCode))
                    TempData["PendingInvoiceTicketCode"] = ticketCode.Trim();
                return RedirectToAction("LegalProfile", "Agency");
            }

            var (status, ticket, existing) = await _invoices.ResolveEligibleTicketAsync(ticketCode, agency.Id, cancellationToken);
            if (status == TicketInvoiceEligibility.AlreadyInvoiced && existing != null)
            {
                TempData["Success"] = "برای این بلیط قبلاً فاکتور صادر شده است.";
                return RedirectToAction(nameof(Print), new { id = existing.Id });
            }
            if (status != TicketInvoiceEligibility.Ok || ticket == null)
            {
                TempData["Error"] = EligibilityMessage(status);
                return RedirectToAction(nameof(Index));
            }

            var invoice = new OfficialInvoice { AgencyId = agency.Id, CreatedBy = User.Identity?.Name };
            await _invoices.PrefillFromTicketAsync(invoice, ticket, agency.Id);
            try
            {
                var saved = await _invoices.CreateAsync(invoice, cancellationToken);
                TempData["Success"] = "فاکتور " + saved.SerialNumber + " صادر شد.";
                return RedirectToAction(nameof(Print), new { id = saved.Id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id, CancellationToken cancellationToken = default)
        {
            var agency = await ResolveAgencyAsync(cancellationToken);
            if (agency == null) return RedirectToAction("Login", "Auth");
            if (!agency.IsOrganization)
            {
                TempData["Error"] = "فاکتور رسمی فقط برای پنل سازمانی فعال است.";
                return RedirectToAction("Index", "Agency");
            }

            var invoice = await _db.OfficialInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.AgencyId == agency.Id, cancellationToken);
            if (invoice == null) return NotFound();

            // Always show current agency legal/financial profile on print (buyer block).
            ApplyAgencyBuyerProfile(invoice, agency);

            OfficialInvoiceService.HydrateLines(invoice);
            ViewBag.Template = _invoices.GetAssets();
            ViewBag.Seller = await _invoices.GetSellerProfileAsync(cancellationToken);
            ViewData["Title"] = "فاکتور " + invoice.SerialNumber;
            return View(invoice);
        }

        private static void ApplyAgencyBuyerProfile(OfficialInvoice invoice, Agency agency)
        {
            invoice.BuyerName = agency.Name;
            if (!string.IsNullOrWhiteSpace(agency.NationalId))
                invoice.BuyerNationalId = agency.NationalId.Trim();
            if (!string.IsNullOrWhiteSpace(agency.EconomicNo))
                invoice.BuyerEconomicNo = agency.EconomicNo.Trim();
            if (!string.IsNullOrWhiteSpace(agency.RegistrationNo))
                invoice.BuyerRegistrationNo = agency.RegistrationNo.Trim();
            if (!string.IsNullOrWhiteSpace(agency.Address))
                invoice.BuyerAddress = agency.Address.Trim();
            if (!string.IsNullOrWhiteSpace(agency.PhoneNumber))
                invoice.BuyerPhone = agency.PhoneNumber.Trim();
            if (!string.IsNullOrWhiteSpace(agency.Fax))
                invoice.BuyerFax = agency.Fax.Trim();
            if (!string.IsNullOrWhiteSpace(agency.Province))
                invoice.BuyerProvince = agency.Province.Trim();
            if (!string.IsNullOrWhiteSpace(agency.County))
                invoice.BuyerCounty = agency.County.Trim();
            if (!string.IsNullOrWhiteSpace(agency.City))
                invoice.BuyerCity = agency.City.Trim();
            if (!string.IsNullOrWhiteSpace(agency.PostalCode))
                invoice.BuyerPostalCode = agency.PostalCode.Trim();
        }

        private async Task<Agency?> ResolveAgencyAsync(CancellationToken cancellationToken)
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return null;
            return await _db.Agencies.AsNoTracking()
                .Include(a => a.IdentityUser)
                .FirstOrDefaultAsync(a => a.IdentityUser.UserName == username, cancellationToken);
        }

        private static string EligibilityMessage(TicketInvoiceEligibility status) => status switch
        {
            TicketInvoiceEligibility.Cancelled => "برای بلیط لغوشده نمی‌توان فاکتور رسمی صادر کرد.",
            TicketInvoiceEligibility.AlreadyInvoiced => "برای این بلیط قبلاً فاکتور صادر شده است.",
            _ => "این بلیط متعلق به آژانس شما نیست، پیدا نشد، یا قابل صدور فاکتور نیست."
        };

        private static OfficialInvoiceCreateViewModel MapInvoiceToForm(OfficialInvoice invoice)
        {
            var today = DateTime.Today.ToPersianDate();
            return new OfficialInvoiceCreateViewModel
            {
                TicketCode = invoice.TicketCode,
                InvoiceDateShamsi = $"{today.Year:0000}/{today.Month:00}/{today.Day:00}",
                BuyerName = invoice.BuyerName,
                BuyerEconomicNo = invoice.BuyerEconomicNo,
                BuyerRegistrationNo = invoice.BuyerRegistrationNo,
                BuyerProvince = invoice.BuyerProvince,
                BuyerCounty = invoice.BuyerCounty,
                BuyerCity = invoice.BuyerCity,
                BuyerPostalCode = invoice.BuyerPostalCode,
                BuyerAddress = invoice.BuyerAddress,
                BuyerNationalId = invoice.BuyerNationalId,
                BuyerPhone = invoice.BuyerPhone,
                BuyerFax = invoice.BuyerFax,
                PaymentIsCash = invoice.PaymentIsCash,
                Notes = invoice.Notes,
                Lines = invoice.Lines.Select(l => new OfficialInvoiceLineForm
                {
                    ItemCode = l.ItemCode,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    UnitAmountTomans = l.UnitAmountRials / 10,
                    DiscountTomans = l.DiscountRials / 10,
                    VatTomans = l.VatRials / 10
                }).ToList()
            };
        }
    }
}
