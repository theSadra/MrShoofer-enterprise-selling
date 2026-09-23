using Application.Data;
using Application.Models;
using Application.Services.MrShooferORS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
  public interface ITicketStatusSyncService
  {
    /// <summary>
    /// Pull ORS ticket status for recent local tickets and mark IsCancelled when ORS says canceled.
    /// Returns how many tickets were updated.
    /// </summary>
    Task<int> SyncRecentAsync(int agencyId, string? orsApiToken, int maxTickets = 12, CancellationToken cancellationToken = default);

    /// <summary>Sync a specific set of ticket codes for an agency.</summary>
    Task<int> SyncTicketCodesAsync(int agencyId, string? orsApiToken, IEnumerable<string> ticketCodes, CancellationToken cancellationToken = default);
  }

  public class TicketStatusSyncService : ITicketStatusSyncService
  {
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
    private readonly AppDbContext _db;
    private readonly MrShooferAPIClient _api;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TicketStatusSyncService> _logger;

    public TicketStatusSyncService(
      AppDbContext db,
      MrShooferAPIClient api,
      IMemoryCache cache,
      ILogger<TicketStatusSyncService> logger)
    {
      _db = db;
      _api = api;
      _cache = cache;
      _logger = logger;
    }

    public async Task<int> SyncRecentAsync(int agencyId, string? orsApiToken, int maxTickets = 12, CancellationToken cancellationToken = default)
    {
      if (agencyId <= 0 || string.IsNullOrWhiteSpace(orsApiToken))
        return 0;

      var cacheKey = $"ticket-status-sync:{agencyId}";
      if (_cache.TryGetValue(cacheKey, out _))
        return 0;

      var lookback = DateTime.Today.AddDays(-7);
      var codes = await _db.Tickets
        .AsNoTracking()
        .Where(t => EF.Property<int>(t, "AgencyId") == agencyId
                    && !t.IsCancelled
                    && t.RegisteredAt >= lookback
                    && t.TicketCode != null
                    && t.TicketCode != "")
        .OrderByDescending(t => t.RegisteredAt)
        .Select(t => t.TicketCode)
        .Take(Math.Clamp(maxTickets, 1, 30))
        .ToListAsync(cancellationToken);

      var updated = await SyncTicketCodesAsync(agencyId, orsApiToken, codes, cancellationToken);
      _cache.Set(cacheKey, true, CacheTtl);
      return updated;
    }

    public async Task<int> SyncTicketCodesAsync(int agencyId, string? orsApiToken, IEnumerable<string> ticketCodes, CancellationToken cancellationToken = default)
    {
      if (agencyId <= 0 || string.IsNullOrWhiteSpace(orsApiToken))
        return 0;

      var codes = ticketCodes
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Select(c => c.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(30)
        .ToList();
      if (codes.Count == 0)
        return 0;

      _api.SetSellerApiKey(orsApiToken);

      var updated = 0;
      foreach (var code in codes)
      {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
          var info = await _api.GetTicketInfoAsync(code, cancellationToken);
          if (info == null || !info.IsCancelled)
            continue;

          var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t =>
              EF.Property<int>(t, "AgencyId") == agencyId
              && t.TicketCode == code
              && !t.IsCancelled, cancellationToken);
          if (ticket == null)
            continue;

          ticket.IsCancelled = true;
          updated++;
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "Ticket status sync failed for {TicketCode}", code);
        }
      }

      if (updated > 0)
        await _db.SaveChangesAsync(cancellationToken);

      return updated;
    }
  }
}
