namespace Application.Services.Payment
{
  public static class PaymentGatewayRedirect
  {
    /// <summary>
    /// Branded "redirecting to gateway" page — always shown before entering Zarinpal/Shaparak.
    /// </summary>
    public static (string Html, string ContentType) BuildRedirectPage(string targetUrl) =>
      (BuildHtml(targetUrl), "text/html; charset=utf-8");

    /// <summary>
    /// Second hop: try direct bank HTML; fall back to StartPay redirect page.
    /// </summary>
    public static async Task<(string Html, string ContentType)> ResolveEnterGatewayAsync(
      IPaymentService payment,
      string authority)
    {
      var direct = await payment.TryGetDirectGatewayHtmlAsync(authority);
      if (direct.Success && !string.IsNullOrWhiteSpace(direct.Html))
        return (direct.Html, "text/html; charset=utf-8");

      return (BuildHtml(payment.GetPaymentGatewayUrl(authority)), "text/html; charset=utf-8");
    }

    public static string BuildEnterGatewayPath(string authority) =>
      $"/Payments/EnterGateway?authority={Uri.EscapeDataString(authority)}";

    public static string BuildHtml(string targetUrl)
    {
      var attrUrl = targetUrl
        .Replace("&", "&amp;")
        .Replace("\"", "&quot;")
        .Replace("<", "&lt;");
      var jsUrl = targetUrl.Replace("\\", "\\\\").Replace("'", "\\'");
      return "<html lang='fa' dir='rtl'><head><meta charset='UTF-8'>" +
             "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
             "<title>در حال انتقال به درگاه پرداخت...</title>" +
             "<style>body{font-family:Tahoma,sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#f4f4f5}" +
             ".box{text-align:center;background:#fff;padding:2.5rem 2rem;border-radius:1rem;box-shadow:0 8px 28px rgba(24,24,27,.1);border:1px solid #e4e4e7;max-width:22rem;width:calc(100% - 2rem)}" +
             ".spinner{width:44px;height:44px;border:4px solid #e4e4e7;border-top-color:#111111;border-radius:50%;animation:spin .85s linear infinite;margin:0 auto 1.25rem}" +
             "p{margin:0 0 .75rem;color:#18181b;font-size:1.05rem;font-weight:600;line-height:1.6}" +
             "small{display:block;color:#71717a;font-size:.875rem;margin-bottom:1rem}" +
             "a{color:#111111;text-decoration:underline;text-underline-offset:3px;font-size:.875rem}" +
             "@keyframes spin{to{transform:rotate(360deg)}}</style></head>" +
             "<body><div class='box'><div class='spinner'></div>" +
             "<p>در حال انتقال به درگاه پرداخت...</p>" +
             "<small>لطفاً چند لحظه صبر کنید</small>" +
             "<a href='" + attrUrl + "'>اگر منتقل نشدید اینجا کلیک کنید</a></div>" +
             "<script>setTimeout(function(){window.location.replace('" + jsUrl + "');},900);</script></body></html>";
    }
  }
}
