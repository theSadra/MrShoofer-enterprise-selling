namespace Application.Models
{
  /// <summary>
  /// Seller (آژانس) = agency panel; invoices under head-of-passengers; may have commission.
  /// Organization (سازمانی) = org panel; invoices under org name; commission always 0; financial profile required.
  /// </summary>
  public enum AgencyPanelType
  {
    Seller = 0,
    Organization = 1
  }
}
