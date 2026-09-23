using Application.Data;
using Application.Models;
using Application.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests;

public class AgencyPassengerDirectoryTests : IAsyncLifetime
{
  private SqliteConnection _connection = null!;
  private AppDbContext _db = null!;
  private Agency _seller = null!;
  private Agency _org = null!;

  public async Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    await _connection.OpenAsync();
    _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    await _db.Database.EnsureCreatedAsync();

    _seller = new Agency
    {
      Name = "Seller",
      Address = "Tehran",
      AdminMobile = "09120000001",
      DateJoined = DateTime.Now,
      ORSAPI_token = "t",
      Commission = 10,
      PanelType = AgencyPanelType.Seller,
      IdentityUser = new Microsoft.AspNetCore.Identity.IdentityUser { UserName = "seller1" }
    };
    _org = new Agency
    {
      Name = "Org",
      Address = "Tehran",
      AdminMobile = "09120000002",
      DateJoined = DateTime.Now,
      ORSAPI_token = "t",
      Commission = 0,
      PanelType = AgencyPanelType.Organization,
      IdentityUser = new Microsoft.AspNetCore.Identity.IdentityUser { UserName = "org1" }
    };
    _db.Agencies.AddRange(_seller, _org);
    await _db.SaveChangesAsync();
  }

  public async Task DisposeAsync()
  {
    await _db.DisposeAsync();
    await _connection.DisposeAsync();
  }

  [Fact]
  public async Task Seller_saves_new_passenger()
  {
    var dir = new AgencyPassengerDirectory(_db);
    var id = await dir.EnsureSavedForSellerAsync(_seller, "علی", "رضایی", "male", "0012345678", "09121234567");
    Assert.NotNull(id);
    Assert.Equal(1, await _db.AgencyEmployees.CountAsync(e => e.AgencyId == _seller.Id));
  }

  [Fact]
  public async Task Seller_skips_when_fname_lname_id_phone_match()
  {
    var dir = new AgencyPassengerDirectory(_db);
    var first = await dir.EnsureSavedForSellerAsync(_seller, "علی", "رضایی", "male", "0012345678", "09121234567");
    var second = await dir.EnsureSavedForSellerAsync(_seller, "علی", "رضایی", "male", "0012345678", "09121234567");
    Assert.Equal(first, second);
    Assert.Equal(1, await _db.AgencyEmployees.CountAsync(e => e.AgencyId == _seller.Id));
  }

  [Fact]
  public async Task Seller_skips_exact_match_with_phone_normalization()
  {
    _db.AgencyEmployees.Add(new AgencyEmployee
    {
      AgencyId = _seller.Id,
      Firstname = "علی",
      Lastname = "رضایی",
      Gender = "male",
      NaCode = "0012345678",
      PhoneNumber = "09121234567",
      CreatedAt = DateTime.Now,
      UpdatedAt = DateTime.Now
    });
    await _db.SaveChangesAsync();

    var dir = new AgencyPassengerDirectory(_db);
    var id = await dir.EnsureSavedForSellerAsync(_seller, "علی", "رضایی", "male", "0012345678", "989121234567");
    Assert.NotNull(id);
    Assert.Equal(1, await _db.AgencyEmployees.CountAsync(e => e.AgencyId == _seller.Id));
  }

  [Fact]
  public async Task Organization_does_not_auto_save()
  {
    var dir = new AgencyPassengerDirectory(_db);
    var id = await dir.EnsureSavedForSellerAsync(_org, "علی", "رضایی", "male", "0012345678", "09121234567");
    Assert.Null(id);
    Assert.Equal(0, await _db.AgencyEmployees.CountAsync());
  }
}
