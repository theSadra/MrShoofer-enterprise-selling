using Application.Data;
using Application.Services;
using Application.Services.Auth;
using Application.Services.MrShooferORS;
using Application.Services.Payment;
using Kavenegar;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO.Compression;
using System.Threading.RateLimiting;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<DirectionsRepository, DirectionsRepository>();
builder.Services.AddSingleton<DirectionsTravelTimeCalculator>();

builder.Services.AddHttpClient<MrShooferAPIClient>((sp, client) =>
{
  var config = sp.GetRequiredService<IConfiguration>();
  var serviceUrl = config["serivce_url"] ?? "https://mrshoofer.ir";
  client.BaseAddress = new Uri(serviceUrl);
  client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddTransient<CustomerServiceSmsSender>();

builder.Services
  .AddControllersWithViews()
  .AddJsonOptions(opts =>
  {
    // Ensure Persian characters are not escaped in JSON responses
    opts.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
  });


builder.Services.TryAddTransient<IOtpLogin, KavehNeagerOtp>();

builder.Services.AddResponseCompression(options =>
{
  options.EnableForHttps = true;
  options.Providers.Add<BrotliCompressionProvider>();
  options.Providers.Add<GzipCompressionProvider>();
  options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
  {
    "application/json",
    "text/javascript",
    "application/javascript",
    "image/svg+xml"
  });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddHttpClient<IPaymentService, ZarinpalService>(client =>
{
  client.Timeout = TimeSpan.FromSeconds(15);
});

// Configure EF Core to use PostgreSQL via Npgsql and read the proper connection string per environment
var connStringName = builder.Environment.IsDevelopment() ? "development" : "production";
var pgsqlConnString = builder.Configuration.GetConnectionString(connStringName);

builder.Services.AddDbContext<AppDbContext>(options =>
{
  options.UseNpgsql(pgsqlConnString);
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
  options.Password.RequiredLength = 6;
  options.Password.RequireDigit = false;
  options.Password.RequireNonAlphanumeric = false;
  options.Password.RequireUppercase = false;
  options.Password.RequiredUniqueChars = 0;
  options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("Agency", policy =>
      policy.RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == "Role" && c.Value == "Agency") ||
        context.User.IsInRole("Agency")));

  options.AddPolicy("Buyer", policy =>
      policy.RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == "Role" && c.Value == "Buyer") ||
        context.User.IsInRole("Buyer")));

  options.AddPolicy("Admin", policy =>
      policy.RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == "Role" && c.Value == "Admin") ||
        context.User.IsInRole("Admin")));
});

builder.Services.ConfigureApplicationCookie(options =>
{
  options.AccessDeniedPath = "/Auth/AccessDenied";
  options.Cookie.Name = "YourAppCookieName";
  options.Cookie.HttpOnly = true;
  options.ExpireTimeSpan = TimeSpan.FromDays(75);
  options.LoginPath = "/Auth/Login";
  options.Events = new CookieAuthenticationEvents
  {
    OnRedirectToLogin = context =>
    {
      if (context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
      {
        var returnUrl = Uri.EscapeDataString($"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        context.Response.Redirect($"/Admin/Login?ReturnUrl={returnUrl}");
        return Task.CompletedTask;
      }

      context.Response.Redirect(context.RedirectUri);
      return Task.CompletedTask;
    },
    OnRedirectToAccessDenied = context =>
    {
      if (context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
      {
        var returnUrl = Uri.EscapeDataString($"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        context.Response.Redirect($"/Admin/Login?ReturnUrl={returnUrl}");
        return Task.CompletedTask;
      }

      context.Response.Redirect(context.RedirectUri);
      return Task.CompletedTask;
    }
  };

  options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
  options.SlidingExpiration = true;
});

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
  options.AddPolicy("ContactUsPolicy", context =>
      RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
          factory: partition => new FixedWindowRateLimiterOptions
          {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
          }));
});


var app = builder.Build();

// Ensure a default admin account exists for admin panel login.
using (var scope = app.Services.CreateScope())
{
  var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
  var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

  const string adminUsername = "mrshoofer1405";
  const string adminPassword = "qazwsx";

  var adminUser = await userManager.FindByNameAsync(adminUsername);

  if (adminUser == null)
  {
    adminUser = new IdentityUser
    {
      UserName = adminUsername
    };

    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
    if (!createResult.Succeeded)
    {
      throw new Exception($"Failed to create default admin user '{adminUsername}'.");
    }
  }
  else
  {
    var hasPassword = await userManager.HasPasswordAsync(adminUser);
    if (hasPassword)
    {
      var removePasswordResult = await userManager.RemovePasswordAsync(adminUser);
      if (!removePasswordResult.Succeeded)
      {
        throw new Exception($"Failed to reset password for default admin user '{adminUsername}'.");
      }
    }

    var addPasswordResult = await userManager.AddPasswordAsync(adminUser, adminPassword);
    if (!addPasswordResult.Succeeded)
    {
      throw new Exception($"Failed to set password for default admin user '{adminUsername}'.");
    }
  }

  // Keep compatibility with both role-based and claim-based checks.
  if (!await roleManager.RoleExistsAsync("Admin"))
  {
    var roleResult = await roleManager.CreateAsync(new IdentityRole("Admin"));
    if (!roleResult.Succeeded)
    {
      throw new Exception("Failed to create Admin role.");
    }
  }

  if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
  {
    var addRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
    if (!addRoleResult.Succeeded)
    {
      throw new Exception($"Failed to assign Admin role to '{adminUsername}'.");
    }
  }

  var claims = await userManager.GetClaimsAsync(adminUser);
  if (!claims.Any(c => c.Type == "Role" && c.Value == "Admin"))
  {
    var claimResult = await userManager.AddClaimAsync(adminUser, new System.Security.Claims.Claim("Role", "Admin"));
    if (!claimResult.Succeeded)
    {
      throw new Exception($"Failed to assign Admin claim to '{adminUsername}'.");
    }
  }
}

app.UseResponseCompression();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
  app.UseDeveloperExceptionPage();
}
else
{
  app.UseExceptionHandler("/Home/Error");
  app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
  OnPrepareResponse = ctx =>
  {
    ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800"); // 7 days
  }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
  var user = context.User;
  var isAuthenticated = user?.Identity?.IsAuthenticated == true;
  var isAdmin = isAuthenticated &&
                (user!.HasClaim(c => c.Type == "Role" && c.Value == "Admin") || user.IsInRole("Admin"));

  if (isAdmin)
  {
    var path = context.Request.Path;
    var isAdminPath = path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase);
    var isAllowedSharedPath =
      path.StartsWithSegments("/Auth/Logout", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase);

    if (!isAdminPath && !isAllowedSharedPath)
    {
      context.Response.Redirect("/Admin");
      return;
    }
  }

  await next();
});

app.MapControllerRoute(
    name: "agency",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "AgencyArea" });

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
