using System.Globalization;
using System.Text.Json;
using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.OfficialInvoices
{
    public class InvoiceSerialSettings
    {
        public string Pattern { get; set; } = "{n}-{prefix}";
        public string Prefix { get; set; } = "2209";
        public int NextSequence { get; set; } = 1;
        public string Preview { get; set; } = "1-2209";
    }

    public static class OfficialInvoiceSeller
    {
        public const string CompanyName = "گروه فناوری رهنگار";
        public const string EconomicNo = "14015483891";
        public const string RegistrationNo = "676497";
        public const string NationalId = "14015483891";
        public const string Phone = "021-28422243";
        public const string Province = "تهران";
        public const string County = "تهران";
        public const string City = "تهران";
        public const string PostalCode = "1815767431";
        public const string Address = "تهران میرداماد میدان مادر خیابان شاه نظری کوچه ابن سینا پلاک ۲";
        public const string Title = "صورتحساب فروش خدمات از مسترشوفر (گروه فناوری رهنگار)";
        public const string MoodianNote =
            "این فاکتور ظرف مدت ۵ روز کاری پس از صدور، در سامانه مودیان بارگزاری خواهد شد";
    }

    public class InvoiceTemplateAssets
    {
        public string LogoUrl { get; set; } = "/logo_full_b.png";
        public string StampUrl { get; set; } = "/invoice/rahnegar-stamp.png";
    }

    public enum TicketInvoiceEligibility
    {
        Ok = 0,
        NotFound = 1,
        Cancelled = 2,
        AlreadyInvoiced = 3
    }

    public interface IOfficialInvoiceService
    {
        InvoiceSerialSettings GetSerialSettings();
        string FormatSerial(string pattern, string prefix, int sequence, DateTime at);
        Task<OfficialInvoice> CreateAsync(OfficialInvoice invoice, CancellationToken cancellationToken = default);
        Task<(TicketInvoiceEligibility Status, Ticket? Ticket, OfficialInvoice? ExistingInvoice)> ResolveEligibleTicketAsync(
            string ticketCode, int agencyId, CancellationToken cancellationToken = default);
        Task PrefillFromTicketAsync(OfficialInvoice invoice, Ticket ticket, int agencyId);
        Task<OfficialInvoice?> SetUploadStatusAsync(
            int invoiceId, OfficialInvoiceUploadStatus status, string? changedBy, CancellationToken cancellationToken = default);
        Task<InvoiceSellerProfile> GetSellerProfileAsync(CancellationToken cancellationToken = default);
        Task<InvoiceSellerProfile> SaveSellerProfileAsync(InvoiceSellerProfile profile, CancellationToken cancellationToken = default);
        InvoiceTemplateAssets GetAssets();
    }

    public class OfficialInvoiceService : IOfficialInvoiceService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public OfficialInvoiceService(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public InvoiceTemplateAssets GetAssets()
        {
            var root = _env.WebRootPath ?? "wwwroot";
            var stamp = Path.Combine(root, "invoice", "rahnegar-stamp.png");
            var logo = Path.Combine(root, "logo_full_b.png");
            var stampTick = File.Exists(stamp) ? File.GetLastWriteTimeUtc(stamp).Ticks : 0;
            var logoTick = File.Exists(logo) ? File.GetLastWriteTimeUtc(logo).Ticks : 0;
            return new InvoiceTemplateAssets
            {
                LogoUrl = $"/logo_full_b.png?v={logoTick}",
                StampUrl = $"/invoice/rahnegar-stamp.png?v={stampTick}"
            };
        }

        public InvoiceSerialSettings GetSerialSettings()
        {
            var s = _db.InvoiceSerialConfigs.AsNoTracking().FirstOrDefault();
            var settings = new InvoiceSerialSettings
            {
                Pattern = string.IsNullOrWhiteSpace(s?.Pattern) ? "{n}-{prefix}" : s.Pattern.Trim(),
                Prefix = string.IsNullOrWhiteSpace(s?.Prefix) ? "2209" : s.Prefix.Trim(),
                NextSequence = s == null || s.NextSequence < 1 ? 1 : s.NextSequence
            };
            settings.Preview = FormatSerial(settings.Pattern, settings.Prefix, settings.NextSequence, DateTime.Now);
            return settings;
        }

        public string FormatSerial(string pattern, string prefix, int sequence, DateTime at)
        {
            var pc = new PersianCalendar();
            var yyyy = pc.GetYear(at).ToString(CultureInfo.InvariantCulture);
            var text = (pattern ?? "{n}-{prefix}")
                .Replace("{yyyy}", yyyy, StringComparison.OrdinalIgnoreCase)
                .Replace("{yy}", yyyy.Length >= 2 ? yyyy[^2..] : yyyy, StringComparison.OrdinalIgnoreCase)
                .Replace("{prefix}", prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{n}", sequence.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            return text.Trim();
        }

        public async Task<OfficialInvoice> CreateAsync(OfficialInvoice invoice, CancellationToken cancellationToken = default)
        {
            invoice.Lines ??= new List<OfficialInvoiceLine>();
            invoice.Lines = invoice.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Description) || l.UnitAmountRials > 0)
                .ToList();
            Recalc(invoice);

            if (invoice.TicketId == null || string.IsNullOrWhiteSpace(invoice.TicketCode))
                throw new InvalidOperationException("Official invoice requires a purchased ticket.");
            if (invoice.Lines.Count == 0 || invoice.PayableRials <= 0)
                throw new InvalidOperationException("Official invoice requires a non-zero service line from the ticket.");

            invoice.UploadStatus = OfficialInvoiceUploadStatus.Pending;
            invoice.UploadedAt = null;
            invoice.UploadedBy = null;
            invoice.Notes = OfficialInvoiceSeller.MoodianNote;

            var s = await _db.InvoiceSerialConfigs.FirstOrDefaultAsync(cancellationToken);
            if (s == null)
            {
                s = new InvoiceSerialConfig();
                _db.InvoiceSerialConfigs.Add(s);
            }
            var pattern = string.IsNullOrWhiteSpace(s.Pattern) ? "{n}-{prefix}" : s.Pattern.Trim();
            var prefix = string.IsNullOrWhiteSpace(s.Prefix) ? "2209" : s.Prefix.Trim();
            var seq = s.NextSequence < 1 ? 1 : s.NextSequence;
            invoice.SerialNumber = FormatSerial(pattern, prefix, seq, invoice.InvoiceDate == default ? DateTime.Now : invoice.InvoiceDate);
            s.NextSequence = seq + 1;

            if (invoice.InvoiceDate == default) invoice.InvoiceDate = DateTime.Today;
            invoice.CreatedAt = DateTime.Now;
            invoice.LinesJson = JsonSerializer.Serialize(invoice.Lines, JsonOpts);

            _db.OfficialInvoices.Add(invoice);
            await _db.SaveChangesAsync(cancellationToken);
            return invoice;
        }

        public async Task<(TicketInvoiceEligibility Status, Ticket? Ticket, OfficialInvoice? ExistingInvoice)> ResolveEligibleTicketAsync(
            string ticketCode, int agencyId, CancellationToken cancellationToken = default)
        {
            ticketCode = (ticketCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ticketCode))
                return (TicketInvoiceEligibility.NotFound, null, null);

            var ticket = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketCode == ticketCode
                                          && EF.Property<int>(t, "AgencyId") == agencyId, cancellationToken);
            if (ticket == null)
                return (TicketInvoiceEligibility.NotFound, null, null);
            if (ticket.IsCancelled)
                return (TicketInvoiceEligibility.Cancelled, ticket, null);

            var existing = await _db.OfficialInvoices.AsNoTracking()
                .Where(i => i.AgencyId == agencyId && i.TicketId == ticket.Id)
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing != null)
                return (TicketInvoiceEligibility.AlreadyInvoiced, ticket, existing);

            return (TicketInvoiceEligibility.Ok, ticket, null);
        }

        public async Task PrefillFromTicketAsync(OfficialInvoice invoice, Ticket ticket, int agencyId)
        {
            await PrefillFromTicketInternalAsync(invoice, ticket, agencyId);
        }

        private async Task PrefillFromTicketInternalAsync(OfficialInvoice invoice, Ticket ticket, int agencyId)
        {
            invoice.TicketId = ticket.Id;
            invoice.TicketCode = ticket.TicketCode;
            invoice.AgencyId = agencyId;
            invoice.InvoiceDate = DateTime.Today;
            invoice.PaymentIsCash = true;
            invoice.Notes = OfficialInvoiceSeller.MoodianNote;
            invoice.UploadStatus = OfficialInvoiceUploadStatus.Pending;
            invoice.UploadedAt = null;
            invoice.UploadedBy = null;

            var agency = await _db.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agencyId);
            var isOrg = agency?.IsOrganization == true;

            // Organization: buyer = agency legal profile.
            // Agency (seller): buyer = ticket passenger (no agency financial IDs).
            var passengerName = $"{ticket.Firstname} {ticket.Lastname}".Trim();
            if (isOrg && agency != null)
            {
                invoice.BuyerName = agency.Name;
                invoice.BuyerNationalId = NullIfBlank(agency.NationalId) ?? ticket.NaCode;
                invoice.BuyerEconomicNo = NullIfBlank(agency.EconomicNo);
                invoice.BuyerRegistrationNo = NullIfBlank(agency.RegistrationNo);
                invoice.BuyerAddress = NullIfBlank(agency.Address);
                invoice.BuyerPhone = NullIfBlank(agency.PhoneNumber) ?? ticket.PhoneNumber;
                invoice.BuyerFax = NullIfBlank(agency.Fax);
                invoice.BuyerProvince = NullIfBlank(agency.Province);
                invoice.BuyerCounty = NullIfBlank(agency.County);
                invoice.BuyerCity = NullIfBlank(agency.City);
                invoice.BuyerPostalCode = NullIfBlank(agency.PostalCode);
            }
            else
            {
                invoice.BuyerName = passengerName;
                invoice.BuyerNationalId = NullIfBlank(ticket.NaCode);
                invoice.BuyerEconomicNo = null;
                invoice.BuyerRegistrationNo = null;
                invoice.BuyerAddress = null;
                invoice.BuyerPhone = NullIfBlank(ticket.PhoneNumber);
                invoice.BuyerFax = null;
                invoice.BuyerProvince = null;
                invoice.BuyerCounty = null;
                invoice.BuyerCity = null;
                invoice.BuyerPostalCode = null;
            }

            var rials = (long)ticket.TicketFinalPrice * 10;
            var serviceBit = string.IsNullOrWhiteSpace(ticket.ServiceName) ? "" : $" ({ticket.ServiceName})";
            invoice.Lines = new List<OfficialInvoiceLine>
            {
                new()
                {
                    ItemCode = ticket.TicketCode,
                    Description = $"بابت سفر {ticket.TripOrigin} به {ticket.TripDestination}{serviceBit} با کد بلیط {ticket.TicketCode} به مبلغ {rials.ToString("N0")} ریال. برای مسافر: {passengerName}",
                    Quantity = 1,
                    Unit = "سفر",
                    UnitAmountRials = rials
                }
            };
            Recalc(invoice);
        }

        public async Task<OfficialInvoice?> SetUploadStatusAsync(
            int invoiceId, OfficialInvoiceUploadStatus status, string? changedBy, CancellationToken cancellationToken = default)
        {
            var invoice = await _db.OfficialInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice == null) return null;

            invoice.UploadStatus = status;
            if (status == OfficialInvoiceUploadStatus.Uploaded)
            {
                invoice.UploadedAt = DateTime.Now;
                invoice.UploadedBy = changedBy;
            }
            else
            {
                invoice.UploadedAt = null;
                invoice.UploadedBy = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return invoice;
        }

        public async Task<InvoiceSellerProfile> GetSellerProfileAsync(CancellationToken cancellationToken = default)
        {
            var profile = await _db.InvoiceSellerProfiles.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
            if (profile != null) return profile;

            profile = new InvoiceSellerProfile();
            _db.InvoiceSellerProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        public async Task<InvoiceSellerProfile> SaveSellerProfileAsync(InvoiceSellerProfile input, CancellationToken cancellationToken = default)
        {
            var profile = await _db.InvoiceSellerProfiles.OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
            if (profile == null)
            {
                profile = new InvoiceSellerProfile();
                _db.InvoiceSellerProfiles.Add(profile);
            }

            profile.CompanyName = (input.CompanyName ?? string.Empty).Trim();
            profile.EconomicNo = (input.EconomicNo ?? string.Empty).Trim();
            profile.RegistrationNo = (input.RegistrationNo ?? string.Empty).Trim();
            profile.NationalId = (input.NationalId ?? string.Empty).Trim();
            profile.Phone = (input.Phone ?? string.Empty).Trim();
            profile.Province = (input.Province ?? string.Empty).Trim();
            profile.County = (input.County ?? string.Empty).Trim();
            profile.City = (input.City ?? string.Empty).Trim();
            profile.PostalCode = (input.PostalCode ?? string.Empty).Trim();
            profile.Address = (input.Address ?? string.Empty).Trim();
            profile.InvoiceTitle = (input.InvoiceTitle ?? string.Empty).Trim();

            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        public static void HydrateLines(OfficialInvoice invoice)
        {
            if (invoice.Lines is { Count: > 0 }) return;
            if (string.IsNullOrWhiteSpace(invoice.LinesJson))
            {
                invoice.Lines = new List<OfficialInvoiceLine>();
                return;
            }
            invoice.Lines = JsonSerializer.Deserialize<List<OfficialInvoiceLine>>(invoice.LinesJson, JsonOpts)
                            ?? new List<OfficialInvoiceLine>();
        }

        public static void Recalc(OfficialInvoice invoice)
        {
            invoice.Lines ??= new List<OfficialInvoiceLine>();
            invoice.TotalRials = invoice.Lines.Sum(l => l.LineTotalRials);
            invoice.DiscountRials = invoice.Lines.Sum(l => l.DiscountRials);
            invoice.VatRials = invoice.Lines.Sum(l => l.VatRials);
            invoice.PayableRials = invoice.Lines.Sum(l => l.PayableRials);
        }

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
