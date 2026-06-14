using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Models.Payment
{
  public class ZarinpalPaymentRequest
  {
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("callback_url")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("metadata")]
    public ZarinpalMetadata Metadata { get; set; }
  }

  public class ZarinpalMetadata
  {
    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
  }

  public class ZarinpalPaymentResponse
  {
    [JsonPropertyName("data")]
    public ZarinpalResponseData? Data { get; set; }

    [JsonPropertyName("errors")]
    public object? Errors { get; set; }

    public ZarinpalResponseData? GetErrorData()
    {
      if (Errors is JsonElement el && el.ValueKind == JsonValueKind.Object)
        return JsonSerializer.Deserialize<ZarinpalResponseData>(el.GetRawText());
      return null;
    }
  }

  public class ZarinpalResponseData
  {
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("authority")]
    public string? Authority { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
  }

  public class ZarinpalVerifyRequest
  {
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("authority")]
    public string Authority { get; set; }
  }

  public class ZarinpalVerifyResponse
  {
    [JsonPropertyName("data")]
    public ZarinpalVerifyData? Data { get; set; }

    [JsonPropertyName("errors")]
    public object? Errors { get; set; }

    public ZarinpalVerifyData? GetErrorData()
    {
      if (Errors is JsonElement el && el.ValueKind == JsonValueKind.Object)
        return JsonSerializer.Deserialize<ZarinpalVerifyData>(el.GetRawText());
      return null;
    }
  }

  public class ZarinpalVerifyData
  {
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("ref_id")]
    public long RefId { get; set; }

    [JsonPropertyName("card_pan")]
    public string? CardPan { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
  }
}
