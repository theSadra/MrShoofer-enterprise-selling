using Application.Data;
using Application.Models;
using Application.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.Tests;

/// <summary>
/// Live sms.ir check: notifies AdminNotifyPhone when a contact message is created.
/// </summary>
public class ContactMessageAdminSmsTests : IAsyncLifetime
{
  private SqliteConnection _connection = null!;
  private IConfiguration _config = null!;

  public Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();
    _config = LoadAppSettings();
    return Task.CompletedTask;
  }

  public async Task DisposeAsync()
  {
    await _connection.DisposeAsync();
  }

  [Fact]
  public async Task Contact_message_persists_and_real_admin_sms_is_sent()
  {
    Assert.False(string.IsNullOrWhiteSpace(_config["smsirapikey"]), "smsirapikey missing");
    Assert.Equal("09902063015", _config["AdminNotifyPhone"]);
    Assert.False(string.IsNullOrWhiteSpace(_config["smsirlinenumber"]), "smsirlinenumber missing");

    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseSqlite(_connection)
      .Options;

    await using var db = new AppDbContext(options);
    await db.Database.EnsureCreatedAsync();

    var sms = new CustomerServiceSmsSender(_config, NullLogger<CustomerServiceSmsSender>.Instance);
    var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    var msg = new ContactUsMessage
    {
      Name = "تست ادمین",
      Number = "09120000000",
      Message = $"پیام تست عملکردی sms.ir — {stamp}",
      RegisteredDateTime = DateTime.Now
    };

    // Real sms.ir BulkSend to AdminNotifyPhone — throws if status != 1.
    await sms.NotifyAdminNewContactMessageAsync(msg.Name, msg.Number, msg.Message);

    db.ContactMessages.Add(msg);
    await db.SaveChangesAsync();

    var saved = await db.ContactMessages.SingleAsync();
    Assert.Equal("تست ادمین", saved.Name);
    Assert.Equal("09120000000", saved.Number);
    Assert.Contains("پیام تست عملکردی", saved.Message);
  }

  private static IConfiguration LoadAppSettings()
  {
    var candidates = new[]
    {
      Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "appsettings.json")),
      Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")),
      Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "appsettings.json")),
      "/Users/amirali/MrShoofer-enterprise-selling/appsettings.json"
    };

    var path = candidates.FirstOrDefault(File.Exists)
      ?? throw new FileNotFoundException("appsettings.json not found for live SMS test", string.Join(", ", candidates));

    return new ConfigurationBuilder()
      .AddJsonFile(path, optional: false)
      .Build();
  }
}
