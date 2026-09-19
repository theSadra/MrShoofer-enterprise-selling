using Application.Data;
using Application.Models;
using Application.Services.OfficialInvoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "Admin")]
    [Route("Admin/OfficialInvoices")]
    public class OfficialInvoicesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IOfficialInvoiceService _invoices;

        public OfficialInvoicesController(AppDbContext db, IOfficialInvoiceService invoices)
        {
            _db = db;
            _invoices = invoices;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken = default)
        {
            ViewData["Title"] = "فاکتورهای رسمی (سامانه مودیان)";
            var query = _db.OfficialInvoices.AsNoTracking()
                .Include(i => i.Agency)
                .AsQueryable();

            if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.UploadStatus == OfficialInvoiceUploadStatus.Pending);
            else if (string.Equals(status, "uploaded", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.UploadStatus == OfficialInvoiceUploadStatus.Uploaded);

            var list = await query
                .OrderByDescending(i => i.Id)
                .Take(500)
                .ToListAsync(cancellationToken);

            foreach (var inv in list)
                OfficialInvoiceService.HydrateLines(inv);

            ViewBag.Filter = status ?? "all";
            ViewBag.PendingCount = await _db.OfficialInvoices.CountAsync(
                i => i.UploadStatus == OfficialInvoiceUploadStatus.Pending, cancellationToken);
            ViewBag.Seller = await _invoices.GetSellerProfileAsync(cancellationToken);
            return View(list);
        }

        [HttpPost("SetStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, int status, CancellationToken cancellationToken = default)
        {
            if (id <= 0 || (status != (int)OfficialInvoiceUploadStatus.Pending
                            && status != (int)OfficialInvoiceUploadStatus.Uploaded))
            {
                TempData["Error"] = "وضعیت نامعتبر است.";
                return RedirectToAction(nameof(Index));
            }

            var uploadStatus = (OfficialInvoiceUploadStatus)status;
            var updated = await _invoices.SetUploadStatusAsync(id, uploadStatus, User.Identity?.Name, cancellationToken);
            if (updated == null)
            {
                TempData["Error"] = "فاکتور پیدا نشد.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"وضعیت فاکتور {updated.SerialNumber} به «{updated.UploadStatusText}» تغییر کرد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Print/{id:int}")]
        public async Task<IActionResult> Print(int id, CancellationToken cancellationToken = default)
        {
            var invoice = await _db.OfficialInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (invoice == null) return NotFound();

            OfficialInvoiceService.HydrateLines(invoice);
            ViewBag.Template = _invoices.GetAssets();
            ViewBag.Seller = await _invoices.GetSellerProfileAsync(cancellationToken);
            ViewBag.IsAdminPrint = true;
            ViewData["Title"] = "فاکتور " + invoice.SerialNumber;
            return View("~/Areas/AgencyArea/Views/OfficialInvoices/Print.cshtml", invoice);
        }

        [HttpGet("Seller")]
        public async Task<IActionResult> Seller(CancellationToken cancellationToken = default)
        {
            ViewData["Title"] = "تنظیمات فاکتور";
            var profile = await _invoices.GetSellerProfileAsync(cancellationToken);
            return View(profile);
        }

        [HttpPost("Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Seller(InvoiceSellerProfile model, CancellationToken cancellationToken = default)
        {
            ViewData["Title"] = "تنظیمات فاکتور";
            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                ModelState.AddModelError(nameof(model.CompanyName), "نام کامل سازمان الزامی است.");
                return View(model);
            }

            await _invoices.SaveSellerProfileAsync(model, cancellationToken);
            TempData["Success"] = "تنظیمات فاکتور ذخیره شد.";
            return RedirectToAction(nameof(Seller));
        }
    }
}
