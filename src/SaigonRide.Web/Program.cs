using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using SaigonRide.Data;
using SaigonRide.Data.Seed;
using SaigonRide.Services;
using SaigonRide.Services.Auth;
using SaigonRide.Web.Infrastructure;
using Serilog;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext());

// --- Layered services ----------------------------------------------------
builder.Services.AddSaigonRideData(builder.Configuration);
builder.Services.AddSaigonRideServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Auth (Cookie) -------------------------------------------------------
var authScheme = builder.Configuration["Security:CookieAuthenticationScheme"] ?? CookieAuthenticationDefaults.AuthenticationScheme;
builder.Services
    .AddAuthentication(authScheme)
    .AddCookie(authScheme, options =>
    {
        options.LoginPath = builder.Configuration["Security:LoginPath"] ?? "/Account/Login";
        options.AccessDeniedPath = builder.Configuration["Security:AccessDeniedPath"] ?? "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

// --- MVC + i18n ----------------------------------------------------------
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("vi"), new CultureInfo("ko") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Add explicitly to ensure priority
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider { CookieName = CookieRequestCultureProvider.DefaultCookieName });
    options.RequestCultureProviders.Add(new UserRoleCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

// --- Pipeline ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestLocalization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- Database init + seed ------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<SaigonRideDbContext>();
    var hasher = sp.GetRequiredService<IPasswordHasher>();

    if (db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
    await DbSeeder.SeedAsync(db, hasher.Hash);
}

app.Run();

public partial class Program { }
