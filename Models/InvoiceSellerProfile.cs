using System.ComponentModel.DataAnnotations;

namespace Application.Models
{
    /// <summary>
    /// Profile of the invoice issuer (فروشنده صورتحساب) used on official invoices / Moodian.
    /// Singleton row — admin edits via OfficialInvoices seller settings.
    /// </summary>
    public class InvoiceSellerProfile
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string CompanyName { get; set; } = "گروه فناوری رهنگار";

        [MaxLength(32)]
        public string EconomicNo { get; set; } = "14015483891";

        [MaxLength(32)]
        public string RegistrationNo { get; set; } = "676497";

        [MaxLength(20)]
        public string NationalId { get; set; } = "14015483891";

        [MaxLength(32)]
        public string Phone { get; set; } = "021-28422243";

        [MaxLength(64)]
        public string Province { get; set; } = "تهران";

        [MaxLength(64)]
        public string County { get; set; } = "تهران";

        [MaxLength(64)]
        public string City { get; set; } = "تهران";

        [MaxLength(20)]
        public string PostalCode { get; set; } = "1815767431";

        [MaxLength(500)]
        public string Address { get; set; } =
            "تهران میرداماد میدان مادر خیابان شاه نظری کوچه ابن سینا پلاک ۲";

        [MaxLength(300)]
        public string InvoiceTitle { get; set; } =
            "صورتحساب فروش خدمات از مسترشوفر (گروه فناوری رهنگار)";
    }
}
