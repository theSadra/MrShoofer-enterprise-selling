namespace Application.Services.Payment
{
  public interface IPaymentService
  {
    Task<(bool Success, string Authority, string Message)> RequestPaymentAsync(
        int amountRials, string description, string mobile, string? email = null, string? callbackUrl = null);

    Task<(bool Success, long RefId, string CardPan, string Message)> VerifyPaymentAsync(
        string authority, int amountRials);

    string GetPaymentGatewayUrl(string authority);

    /// <summary>
    /// Skips Zarinpal checkout review by requesting StartPay with checkout Referer server-side.
    /// </summary>
    Task<(bool Success, string Html, string Message)> TryGetDirectGatewayHtmlAsync(string authority);
  }
}
