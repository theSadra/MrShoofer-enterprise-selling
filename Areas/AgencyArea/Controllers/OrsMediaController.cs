using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Areas.AgencyArea
{
  /// <summary>
  /// Same-origin proxy for ORS service-type logos so the browser can process pixels
  /// (ORS does not send CORS headers for /images/…).
  /// </summary>
  [Area("AgencyArea")]
  [AllowAnonymous]
  [Route("media/ors")]
  public class OrsMediaController : Controller
  {
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public OrsMediaController(
      IHttpClientFactory httpClientFactory,
      IConfiguration configuration,
      IMemoryCache cache)
    {
      _httpClientFactory = httpClientFactory;
      _configuration = configuration;
      _cache = cache;
    }

    [HttpGet("{*path}")]
    [ResponseCache(Duration = 43200, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string path, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(path))
        return NotFound();

      var cleaned = path.Trim().TrimStart('/');
      // Allow ORS image tree only (service-types, cars, …) — block path traversal.
      if (cleaned.Contains("..", StringComparison.Ordinal) ||
          cleaned.Contains('\\') ||
          !cleaned.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
      {
        return NotFound();
      }

      var cacheKey = "ors-media:" + cleaned.ToLowerInvariant();
      if (!_cache.TryGetValue(cacheKey, out byte[]? bytes) || bytes == null || bytes.Length == 0)
      {
        var baseUrl = (_configuration["MrShoofer:ApiBaseUrl"]
          ?? _configuration["serivce_url"]
          ?? "https://ors.shoofer.taxi").TrimEnd('/');

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        using var response = await client.GetAsync($"{baseUrl}/{cleaned}", cancellationToken);
        if (!response.IsSuccessStatusCode)
          return NotFound();

        bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
          return NotFound();

        _cache.Set(cacheKey, bytes, CacheTtl);
      }

      var contentType = cleaned.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        ? "image/webp"
        : cleaned.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || cleaned.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
          ? "image/jpeg"
          : "image/png";

      Response.Headers.CacheControl = "public,max-age=43200";
      return File(bytes, contentType);
    }
  }
}
