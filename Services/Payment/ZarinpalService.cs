using Application.Models.Payment;
using System.Text;
using System.Text.Json;

namespace Application.Services.Payment
{
  public class ZarinpalService : IPaymentService
  {
    private readonly HttpClient _httpClient;
    private readonly string _merchantId;
    private readonly string _paymentUrl;
    private readonly string _verifyUrl;
    private readonly string _gatewayUrl;
    private readonly string? _callbackUrl;
    private readonly bool _isSandbox;

    public ZarinpalService(HttpClient httpClient, IConfiguration configuration)
    {
      _httpClient = httpClient;
      _merchantId = configuration["Zarinpal:MerchantId"]!;
      _isSandbox = configuration.GetValue<bool>("Zarinpal:IsSandbox", false);

      _paymentUrl = configuration["Zarinpal:PaymentUrl"]
          ?? (_isSandbox
              ? "https://sandbox.zarinpal.com/pg/v4/payment/request.json"
              : "https://payment.zarinpal.com/pg/v4/payment/request.json");

      _verifyUrl = configuration["Zarinpal:VerifyUrl"]
          ?? (_isSandbox
              ? "https://sandbox.zarinpal.com/pg/v4/payment/verify.json"
              : "https://payment.zarinpal.com/pg/v4/payment/verify.json");

      _gatewayUrl = configuration["Zarinpal:PaymentGatewayUrl"]
          ?? (_isSandbox
              ? "https://sandbox.zarinpal.com/pg/StartPay/"
              : "https://payment.zarinpal.com/pg/StartPay/");

      _callbackUrl = configuration["Zarinpal:CallbackUrl"];
    }

    public async Task<(bool Success, string Authority, string Message)> RequestPaymentAsync(
        int amountRials, string description, string mobile, string? email = null, string? callbackUrl = null)
    {
      var request = new ZarinpalPaymentRequest
      {
        MerchantId = _merchantId,
        Amount = amountRials,
        Description = description,
        CallbackUrl = callbackUrl ?? _callbackUrl ?? string.Empty,
        Metadata = new ZarinpalMetadata { Mobile = mobile, Email = email }
      };

      try
      {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_paymentUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ZarinpalPaymentResponse>(body);

        if (result?.Data?.Code == 100 && !string.IsNullOrEmpty(result.Data.Authority))
          return (true, result.Data.Authority, "موفق");

        var err = result?.GetErrorData();
        return (false, string.Empty, err?.Message ?? $"خطای زرین‌پال (کد: {err?.Code})");
      }
      catch (Exception ex)
      {
        return (false, string.Empty, $"خطا در ارتباط با درگاه: {ex.Message}");
      }
    }

    public async Task<(bool Success, long RefId, string CardPan, string Message)> VerifyPaymentAsync(
        string authority, int amountRials)
    {
      var request = new ZarinpalVerifyRequest
      {
        MerchantId = _merchantId,
        Amount = amountRials,
        Authority = authority
      };

      try
      {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_verifyUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ZarinpalVerifyResponse>(body);

        // 100 = success, 101 = already verified (idempotent — both are OK)
        if (result?.Data?.Code is 100 or 101)
          return (true, result.Data.RefId, result.Data.CardPan ?? string.Empty, "پرداخت تایید شد");

        var err = result?.GetErrorData();
        return (false, 0, string.Empty, err?.Message ?? $"خطای تایید (کد: {err?.Code})");
      }
      catch (Exception ex)
      {
        return (false, 0, string.Empty, $"خطا در تایید پرداخت: {ex.Message}");
      }
    }

    public string GetPaymentGatewayUrl(string authority) => $"{_gatewayUrl}{authority}";
  }
}
