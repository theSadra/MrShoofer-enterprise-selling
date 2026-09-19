namespace Application.Areas.AgencyArea.ViewModels.OfficialInvoices
{
    public class OfficialInvoiceCreateViewModel
    {
        public string? TicketCode { get; set; }
        public string InvoiceDateShamsi { get; set; } = string.Empty;
        public string? BuyerName { get; set; }
        public string? BuyerEconomicNo { get; set; }
        public string? BuyerRegistrationNo { get; set; }
        public string? BuyerProvince { get; set; }
        public string? BuyerCounty { get; set; }
        public string? BuyerCity { get; set; }
        public string? BuyerPostalCode { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerNationalId { get; set; }
        public string? BuyerPhone { get; set; }
        public string? BuyerFax { get; set; }
        public bool PaymentIsCash { get; set; } = true;
        public string? Notes { get; set; } = Application.Services.OfficialInvoices.OfficialInvoiceSeller.MoodianNote;
        public List<OfficialInvoiceLineForm> Lines { get; set; } = new() { new() };
    }

    public class OfficialInvoiceLineForm
    {
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public long UnitAmountTomans { get; set; }
        public long DiscountTomans { get; set; }
        public long VatTomans { get; set; }
    }
}
