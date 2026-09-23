using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
  public interface IAgencyPassengerDirectory
  {
    /// <summary>
    /// For seller (آژانس) panels only: ensure passenger is in the frequent list.
    /// Skips insert when the same national id already exists, or when
    /// Firstname+Lastname+NaCode+Phone already match 100%.
    /// </summary>
    Task<int?> EnsureSavedForSellerAsync(
      Agency agency,
      string? firstname,
      string? lastname,
      string? gender,
      string? naCode,
      string? phoneNumber,
      string? email = null,
      CancellationToken cancellationToken = default);
  }

  public class AgencyPassengerDirectory : IAgencyPassengerDirectory
  {
    private readonly AppDbContext _db;

    public AgencyPassengerDirectory(AppDbContext db)
    {
      _db = db;
    }

    public async Task<int?> EnsureSavedForSellerAsync(
      Agency agency,
      string? firstname,
      string? lastname,
      string? gender,
      string? naCode,
      string? phoneNumber,
      string? email = null,
      CancellationToken cancellationToken = default)
    {
      if (agency == null || agency.IsOrganization)
        return null;

      var agencyId = agency.Id;
      var fn = NormName(firstname);
      var ln = NormName(lastname);
      var na = NormId(naCode);
      var phone = NormPhone(phoneNumber);
      var g = (gender ?? "").Trim().ToLowerInvariant();

      if (fn.Length == 0 || ln.Length == 0 || na.Length == 0 || phone.Length == 0)
        return null;

      if (na.Length != 10 || !na.All(char.IsDigit))
        return null;

      // Fast path: national id is unique per agency.
      var byNa = await _db.AgencyEmployees
        .AsNoTracking()
        .Where(e => e.AgencyId == agencyId && e.NaCode == na)
        .Select(e => new { e.Id, e.Firstname, e.Lastname, e.PhoneNumber })
        .FirstOrDefaultAsync(cancellationToken);

      if (byNa != null)
      {
        // 100% match (or same national id) → do not insert another row.
        return byNa.Id;
      }

      if (string.IsNullOrWhiteSpace(g) || (g != "male" && g != "female"))
        g = "male";

      var now = DateTime.Now;
      var entity = new AgencyEmployee
      {
        AgencyId = agencyId,
        Firstname = fn,
        Lastname = ln,
        Gender = g,
        NaCode = na,
        PhoneNumber = phone,
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
        CreatedAt = now,
        UpdatedAt = now
      };

      _db.AgencyEmployees.Add(entity);
      try
      {
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
      }
      catch (DbUpdateException)
      {
        _db.Entry(entity).State = EntityState.Detached;
        return await _db.AgencyEmployees
          .AsNoTracking()
          .Where(e => e.AgencyId == agencyId && e.NaCode == na)
          .Select(e => (int?)e.Id)
          .FirstOrDefaultAsync(cancellationToken);
      }
    }

    private static string NormName(string? value) => (value ?? "").Trim();

    private static string NormId(string? value)
    {
      var s = (value ?? "").Trim();
      var chars = s.Select(c => c switch
      {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
        _ => c
      }).ToArray();
      return new string(chars);
    }

    private static string NormPhone(string? value)
    {
      var s = NormId(value).Replace(" ", "").Replace("-", "");
      if (s.StartsWith("+98", StringComparison.Ordinal)) s = "0" + s[3..];
      else if (s.StartsWith("98", StringComparison.Ordinal) && s.Length >= 12) s = "0" + s[2..];
      else if (s.StartsWith("9", StringComparison.Ordinal) && s.Length == 10) s = "0" + s;
      return s;
    }
  }
}
