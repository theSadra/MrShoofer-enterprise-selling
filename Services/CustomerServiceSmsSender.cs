using IPE.SmsIrClient;
using IPE.SmsIrClient.Models.Requests;

namespace Application.Services
{
  public class CustomerServiceSmsSender
  {
    private readonly SmsIr smsIr;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomerServiceSmsSender>? _logger;
    private long? _cachedLineNumber;

    public CustomerServiceSmsSender(IConfiguration configuration, ILogger<CustomerServiceSmsSender>? logger = null)
    {
      _configuration = configuration;
      _logger = logger;
      this.smsIr = new SmsIr(configuration["smsirapikey"] ?? string.Empty);
    }

    public async Task SendCustomerTicket_issued(string firstname, string lastname, string reference, string link, string numberphone)
    {
      VerifySendParameter[] verifySendParameters = {
           new VerifySendParameter("FIRSTNAME", firstname),
           new VerifySendParameter("LASTNAME", lastname),
           new VerifySendParameter("TRIP", link),
           new VerifySendParameter("REFERENCE", reference),
        };

      await smsIr.VerifySendAsync(numberphone, 782252, verifySendParameters);
    }

    /// <summary>
    /// Notifies the admin phone that a new contact-us message landed in Admin / پیام‌ها.
    /// </summary>
    public async Task NotifyAdminNewContactMessageAsync(string? name, string number, string message)
    {
      var adminPhone = _configuration["AdminNotifyPhone"];
      if (string.IsNullOrWhiteSpace(adminPhone))
      {
        _logger?.LogWarning("AdminNotifyPhone is not configured; skipping contact-message SMS.");
        return;
      }

      var displayName = string.IsNullOrWhiteSpace(name) ? "بدون نام" : name.Trim();
      var preview = Truncate(message?.Trim() ?? string.Empty, 80);
      var text =
        $"پیام جدید در پیام‌ها\nنام: {displayName}\nشماره: {number}\nمتن: {preview}";

      var lineNumber = await ResolveLineNumberAsync();
      var response = await smsIr.BulkSendAsync(lineNumber, text, new[] { adminPhone });

      if (response?.Status != 1)
      {
        throw new InvalidOperationException(
          $"sms.ir admin notify failed. Status={response?.Status}, Message={response?.Message}");
      }
    }

    private async Task<long> ResolveLineNumberAsync()
    {
      if (_cachedLineNumber.HasValue)
        return _cachedLineNumber.Value;

      var configured = _configuration["smsirlinenumber"];
      if (!string.IsNullOrWhiteSpace(configured) && long.TryParse(configured, out var line))
      {
        _cachedLineNumber = line;
        return line;
      }

      var lines = await smsIr.GetLinesAsync();
      if (lines?.Data == null || lines.Data.Length == 0)
        throw new InvalidOperationException("sms.ir returned no sender lines.");

      _cachedLineNumber = lines.Data[0];
      return _cachedLineNumber.Value;
    }

    private static string Truncate(string value, int maxChars)
    {
      if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        return value;
      return value[..maxChars] + "…";
    }
  }
}
