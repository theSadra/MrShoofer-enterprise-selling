using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Models
{
    public enum OfficialInvoiceUploadStatus : byte
    {
        /// <summary>درحال صدور — not yet uploaded to Moodian.</summary>
        Pending = 0,
        /// <summary>ثبت شده در سامانه مودیان — uploaded/signed on the government site.</summary>
        Uploaded = 1
    }

    public static class OfficialInvoiceUploadStatusText
    {
        public const string Pending = "درحال صدور";
        public const string Uploaded = "ثبت شده در سامانه مودیان";

        public static string ToPersian(OfficialInvoiceUploadStatus status) => status switch
        {
            OfficialInvoiceUploadStatus.Uploaded => Uploaded,
            _ => Pending
        };
    }

    public class OfficialInvoice
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string SerialNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }
        public DateTime CreatedAt { get; set; }

        [MaxLength(256)]
        public string? CreatedBy { get; set; }

        public int AgencyId { get; set; }
        public Agency? Agency { get; set; }

        public int? TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        [MaxLength(32)]
        public string? TicketCode { get; set; }

        [MaxLength(200)]
        public string? BuyerName { get; set; }

        [MaxLength(32)]
        public string? BuyerEconomicNo { get; set; }

        [MaxLength(32)]
        public string? BuyerRegistrationNo { get; set; }

        [MaxLength(64)]
        public string? BuyerProvince { get; set; }

        [MaxLength(64)]
        public string? BuyerCounty { get; set; }

        [MaxLength(64)]
        public string? BuyerCity { get; set; }

        [MaxLength(20)]
        public string? BuyerPostalCode { get; set; }

        [MaxLength(500)]
        public string? BuyerAddress { get; set; }

        [MaxLength(20)]
        public string? BuyerNationalId { get; set; }

        [MaxLength(32)]
        public string? BuyerPhone { get; set; }

        [MaxLength(32)]
        public string? BuyerFax { get; set; }

        public string LinesJson { get; set; } = "[]";

        public long TotalRials { get; set; }
        public long DiscountRials { get; set; }
        public long VatRials { get; set; }
        public long PayableRials { get; set; }

        public bool PaymentIsCash { get; set; } = true;

        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>Moodian upload status — only Admin may change this.</summary>
        public OfficialInvoiceUploadStatus UploadStatus { get; set; } = OfficialInvoiceUploadStatus.Pending;

        public DateTime? UploadedAt { get; set; }

        [MaxLength(256)]
        public string? UploadedBy { get; set; }

        [NotMapped]
        public List<OfficialInvoiceLine> Lines { get; set; } = new();

        [NotMapped]
        public string UploadStatusText => OfficialInvoiceUploadStatusText.ToPersian(UploadStatus);
    }

    public class OfficialInvoiceLine
    {
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public long UnitAmountRials { get; set; }
        public long DiscountRials { get; set; }
        public long VatRials { get; set; }

        public long LineTotalRials => (long)Math.Round((double)Quantity * UnitAmountRials, MidpointRounding.AwayFromZero);
        public long AfterDiscountRials => Math.Max(0, LineTotalRials - DiscountRials);
        public long PayableRials => AfterDiscountRials + VatRials;
    }

    public class InvoiceSerialConfig
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string Pattern { get; set; } = "{n}-{prefix}";

        [MaxLength(32)]
        public string Prefix { get; set; } = "2209";

        public int NextSequence { get; set; } = 1;
    }
}
