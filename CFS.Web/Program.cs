using CFS.Core.Models;
using CFS.Core.Services;
using CFS.Data;
using CFS.Web.Components;
using CFS.Web.Models;
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
builder.Services.AddHttpClient<IAiAdvisorService, OpenAiAdvisorService>();
builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
builder.Services.AddScoped<IExternalInvoiceService, ExternalInvoiceService>();

var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", false);
if (demoEnabled)
{
    builder.Services.AddScoped<ISubscriptionService, StaticSubscriptionService>();
    builder.Services.AddScoped<IAiUsageLimiter, DemoAiUsageLimiter>();
    builder.Services.AddScoped<IOrganizationLabelService, DemoOrganizationLabelService>();
    builder.Services.AddScoped<IBillingRepository, DemoBillingRepository>();
    builder.Services.AddScoped<IExternalInvoiceRepository, DemoExternalInvoiceRepository>();
    builder.Services.AddScoped<ISignupRepository, DemoSignupRepository>();
    builder.Services.AddScoped<IDashboardRepository, DemoDashboardRepository>();
    builder.Services.AddScoped<IUserAuthenticationRepository, DemoUserAuthenticationRepository>();
    builder.Services.AddScoped<ITenantAccessRepository, DemoTenantAccessRepository>();
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
    builder.Services.AddScoped<ISubscriptionService, SqlSubscriptionService>();
    builder.Services.AddScoped<IAiUsageLimiter, SqlAiUsageLimiter>();
    builder.Services.AddScoped<IOrganizationLabelService, SqlOrganizationLabelService>();
    builder.Services.AddScoped<IBillingRepository, SqlBillingRepository>();
    builder.Services.AddScoped<IExternalInvoiceRepository, SqlExternalInvoiceRepository>();
    builder.Services.AddScoped<ISignupRepository, SqlSignupRepository>();
    builder.Services.AddScoped<IDashboardRepository, SqlDashboardRepository>();
    builder.Services.AddScoped<IUserAuthenticationRepository, SqlUserAuthenticationRepository>();
    builder.Services.AddScoped<ITenantAccessRepository, SqlTenantAccessRepository>();
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

app.MapPost("/api/stripe/webhook", async (HttpRequest request, IConfiguration config, ISignupRepository signups, IBillingRepository billing, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("StripeWebhook");
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    var webhookSecret = config["STRIPE_WEBHOOK_SECRET"];

    if (string.IsNullOrWhiteSpace(webhookSecret))
    {
        logger.LogError("STRIPE_WEBHOOK_SECRET is not configured; rejecting webhook request instead of accepting it unverified.");
        return Results.Problem(statusCode: 500);
    }

    Stripe.Event stripeEvent;
    try
    {
        stripeEvent = Stripe.EventUtility.ConstructEvent(json, request.Headers["Stripe-Signature"], webhookSecret);
    }
    catch (Stripe.StripeException ex)
    {
        logger.LogWarning(ex, "Stripe webhook signature verification failed.");
        return Results.BadRequest();
    }

    if (stripeEvent.Type == "checkout.session.completed" &&
        stripeEvent.Data.Object is Stripe.Checkout.Session session)
    {
        var ct = request.HttpContext.RequestAborted;
        var checkoutType = session.Metadata is not null && session.Metadata.TryGetValue("Type", out var t) ? t : null;

        if (checkoutType == "Addon")
        {
            var tenantId = int.Parse(session.Metadata["TenantId"]);
            var addonKey = session.Metadata["AddonKey"];
            var featureKeys = CfsAddons.FeatureKeysByAddon[addonKey];
            await billing.GrantAddonFeaturesAsync(tenantId, featureKeys, session.CustomerId, ct);
        }
        else if (checkoutType == "Upgrade")
        {
            var tenantId = int.Parse(session.Metadata["TenantId"]);
            var newPlanKey = session.Metadata["NewPlanKey"];
            var previousSubscriptionId = await billing.CompleteUpgradeAsync(tenantId, newPlanKey, session.SubscriptionId, session.CustomerId, ct);

            if (!string.IsNullOrWhiteSpace(previousSubscriptionId))
            {
                var secretKey = config["STRIPE_SECRET_KEY"];
                if (!string.IsNullOrWhiteSpace(secretKey))
                {
                    try
                    {
                        var subscriptionService = new Stripe.SubscriptionService(new Stripe.StripeClient(secretKey));
                        await subscriptionService.CancelAsync(previousSubscriptionId, cancellationToken: ct);
                    }
                    catch (Stripe.StripeException ex)
                    {
                        logger.LogWarning(ex, "Failed to cancel previous Stripe subscription {SubscriptionId} after upgrade.", previousSubscriptionId);
                    }
                }
            }
        }
        else
        {
            var tenantId = await signups.CompleteSignupAndProvisionTenantAsync(session.Id, session.CustomerId, session.SubscriptionId, ct);
            if (tenantId is null)
            {
                logger.LogWarning("Stripe checkout.session.completed for session {SessionId} had no matching pending signup to provision.", session.Id);
            }
        }
    }

    return Results.Ok();
}).AllowAnonymous();

app.MapPost("/api/invoices", async (
    HttpRequest httpRequest,
    CreateInvoiceApiRequest body,
    IExternalInvoiceRepository invoiceRepository,
    IExternalInvoiceService invoiceService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("ExternalInvoiceApi");

    var apiKey = httpRequest.Headers["X-Api-Key"].ToString();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(statusCode: 401, title: "Missing X-Api-Key header.");
    }

    var tenant = await invoiceRepository.FindTenantByApiKeyHashAsync(ApiKeyHasher.Hash(apiKey), cancellationToken);
    if (tenant is null)
    {
        logger.LogWarning("Rejected /api/invoices call with unknown or revoked API key.");
        return Results.Problem(statusCode: 401, title: "Invalid API key.");
    }

    if (string.IsNullOrWhiteSpace(body.RecipientName) ||
        string.IsNullOrWhiteSpace(body.RecipientEmail) ||
        string.IsNullOrWhiteSpace(body.Description) ||
        body.Amount <= 0)
    {
        return Results.Problem(statusCode: 400, title: "RecipientName, RecipientEmail, Description, and a positive Amount are required.");
    }

    var amountCents = (int)Math.Round(body.Amount * 100, MidpointRounding.AwayFromZero);
    var currency = string.IsNullOrWhiteSpace(body.Currency) ? "usd" : body.Currency.ToLowerInvariant();

    var result = await invoiceService.CreateAndSendInvoiceAsync(
        new CreateExternalInvoiceRequest(
            tenant.TenantId,
            body.RecipientName,
            body.RecipientEmail,
            amountCents,
            currency,
            body.Description,
            body.ExternalReference),
        cancellationToken);

    var response = new CreateInvoiceApiResponse(
        result.RequestId, result.Status, result.StripeInvoiceId, result.HostedInvoiceUrl, result.ErrorMessage);

    return result.Status == "Failed" ? Results.Json(response, statusCode: 502) : Results.Json(response);
}).AllowAnonymous();

app.MapPost("/api/tenants/api-keys", async (
    CreateApiKeyRequest body,
    ClaimsPrincipal user,
    IExternalInvoiceRepository invoiceRepository,
    CancellationToken cancellationToken) =>
{
    var tenantId = int.Parse(user.FindFirst("TenantId")!.Value);
    var apiKey = ApiKeyHasher.GenerateApiKey();
    await invoiceRepository.CreateApiKeyAsync(tenantId, ApiKeyHasher.Hash(apiKey), body.Label, cancellationToken);
    return Results.Json(new CreateApiKeyResponse(apiKey));
}).RequireAuthorization(policy => policy.RequireRole("Administrador"));

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

app.MapPost("/switch-tenant/{tenantId:int}", async (
    int tenantId,
    ClaimsPrincipal user,
    ITenantAccessRepository tenantAccessRepository,
    HttpContext httpContext) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var accessibleTenants = await tenantAccessRepository.GetAccessibleTenantsAsync(userId, httpContext.RequestAborted);
    var target = accessibleTenants.FirstOrDefault(t => t.TenantId == tenantId);
    if (target is null)
    {
        return Results.Forbid();
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.ToString()),
        new(ClaimTypes.Name, user.FindFirst(ClaimTypes.Name)!.Value),
        new("FullName", user.FindFirst("FullName")!.Value),
        new("TenantId", target.TenantId.ToString()),
        new("TenantName", target.TenantName),
        new("PlanKey", target.PlanKey)
    };
    claims.AddRange(target.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = false });

    return Results.Redirect("/dashboard");
}).RequireAuthorization();

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
