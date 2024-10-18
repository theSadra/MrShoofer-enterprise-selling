using Application.Data;
using Application.Services;
using Application.Services.Auth;
using Application.Services.MrShooferORS;
using Kavenegar;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;



//await smssender.SendCustomerTicket_issued("محمد صدرا".Replace(' ', '\u200C'), "درستکار".Replace(' ', '\u200C'), "Z4EIU", "https://9qbnlgl9-5055.euw.devtunnels.ms/ReserveInfo?reference=Z4EIU", "09902063015");


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<DirectionsRepository, DirectionsRepository>();
builder.Services.AddTransient<MrShooferAPIClient, MrShooferAPIClient>(c => new MrShooferAPIClient(new HttpClient(), "https://mrbilit.mrshoofer.ir"));

builder.Services.AddTransient<KavenegarApi>(k => new KavenegarApi(builder.Configuration["kavehnegar_key"]));
builder.Services.AddTransient<CustomerServiceSmsSender>();


builder.Services.AddControllersWithViews();

builder.Services.TryAddTransient<IOtpLogin, KavehNeagerOtp>();




var sqlite_connstring = builder.Configuration.GetConnectionString("production");
if (builder.Environment.IsDevelopment())
{
  sqlite_connstring = builder.Configuration.GetConnectionString("development");
}

Console.WriteLine("***CONNSTRING : " +  sqlite_connstring);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqlite_connstring));



builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
  options.AccessDeniedPath = "/Auth/AccessDenied";
  options.Cookie.Name = "YourAppCookieName";
  options.Cookie.HttpOnly = true;
  options.ExpireTimeSpan = TimeSpan.FromDays(2);
  options.LoginPath = "/Auth/Login";
  
  options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
  options.SlidingExpiration = true;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization(); 


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
