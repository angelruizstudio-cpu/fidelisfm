using CFS.Core.Models;
using CFS.Core.Services;
using CFS.Data;
using CFS.Web.Components;
using CFS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection()
    .SetApplicationName("CFS.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CFS.Auth";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ISubscriptionService, StaticSubscriptionService>();
builder.Services.AddHttpClient<IAiAdvisorService, OpenAiAdvisorService>();

var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", false);
if (demoEnabled)
{
    builder.Services.AddScoped<IAiUsageLimiter, DemoAiUsageLimiter>();
    builder.Services.AddScoped<IDashboardRepository, DemoDashboardRepository>();
    builder.Services.AddScoped<IUserAuthenticationRepository, DemoUserAuthenticationRepository>();
    builder.Services.AddScoped<IIncomeRepository, DemoIncomeRepository>();
    builder.Services.AddScoped<IExpenseRepository, DemoExpenseRepository>();
    builder.Services.AddScoped<ICheckRepository, DemoCheckRepository>();
    builder.Services.AddScoped<ICheckPrintSettingsRepository, DemoCheckPrintSettingsRepository>();
    builder.Services.AddScoped<IDepositRepository, DemoDepositRepository>();
    builder.Services.AddScoped<IReconciliationRepository, DemoReconciliationRepository>();
    builder.Services.AddScoped<IReportRepository, DemoReportRepository>();
}
else
{
    builder.Services.AddScoped(_ =>
        new SqlConnectionFactory(builder.Configuration.GetConnectionString("CfsDatabase") ?? string.Empty));
    builder.Services.AddScoped<IAiUsageLimiter, SqlAiUsageLimiter>();
    builder.Services.AddScoped<IDashboardRepository, SqlDashboardRepository>();
    builder.Services.AddScoped<IUserAuthenticationRepository, SqlUserAuthenticationRepository>();
    builder.Services.AddScoped<IIncomeRepository, SqlIncomeRepository>();
    builder.Services.AddScoped<IExpenseRepository, SqlExpenseRepository>();
    builder.Services.AddScoped<ICheckRepository, SqlCheckRepository>();
    builder.Services.AddScoped<ICheckPrintSettingsRepository, SqlCheckPrintSettingsRepository>();
    builder.Services.AddScoped<IDepositRepository, SqlDepositRepository>();
    builder.Services.AddScoped<IReconciliationRepository, SqlReconciliationRepository>();
    builder.Services.AddScoped<IReportRepository, SqlReportRepository>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

if (demoEnabled)
{
    app.MapGet("/demo-login", async (HttpContext httpContext) =>
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Name, "demo"),
            new("FullName", "Pastor Demo"),
            new("TenantId", "1"),
            new("TenantName", "Iglesia Cristiana Pentecostes Inc"),
            new("PlanKey", CfsPlans.Founder),
            new(ClaimTypes.Role, "Administrador"),
            new(ClaimTypes.Role, "Finanzas")
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });

        return Results.Redirect("/dashboard");
    }).AllowAnonymous();
}

app.MapPost("/login", async (
    [FromForm] string? userName,
    [FromForm] string? password,
    [FromForm] string? returnUrl,
    IUserAuthenticationRepository users,
    HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
    {
        return Results.Redirect($"/login?error=required&returnUrl={Uri.EscapeDataString(GetSafeReturnUrl(returnUrl))}");
    }

    try
    {
        var user = await users.ValidateCredentialsAsync(userName, password, httpContext.RequestAborted);
        if (user is null)
        {
            return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(GetSafeReturnUrl(returnUrl))}");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("FullName", user.FullName),
            new("TenantId", user.TenantId.ToString()),
            new("TenantName", user.TenantName),
            new("PlanKey", user.PlanKey)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return Results.Redirect(GetSafeReturnUrl(returnUrl));
    }
    catch
    {
        return Results.Redirect($"/login?error=database&returnUrl={Uri.EscapeDataString(GetSafeReturnUrl(returnUrl))}");
    }
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string GetSafeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl) ||
        !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
        returnUrl.StartsWith("//", StringComparison.Ordinal) ||
        returnUrl.Contains("://", StringComparison.Ordinal))
    {
        return "/dashboard";
    }

    return returnUrl;
}
