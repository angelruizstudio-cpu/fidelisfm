using CFS.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CFS.Web.Services;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly int? _tenantId;

    public HttpTenantContext(AuthenticationStateProvider authenticationStateProvider)
    {
        // In Blazor Server, IHttpContextAccessor.HttpContext is only reliable during the
        // initial prerender — once the interactive SignalR circuit takes over it can be
        // null, silently defaulting every tenant-scoped query to tenant 1. The
        // AuthenticationStateProvider cascade is the mechanism Blazor Server guarantees to
        // work across the whole circuit lifetime, so resolve the tenant claim from there.
        var authState = authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
        var claim = authState.User.FindFirst("TenantId")?.Value;
        _tenantId = int.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// The current tenant. Fail-closed: if there is no valid TenantId claim we refuse to serve
    /// a tenant id rather than silently defaulting to tenant 1 (the founder), which would expose
    /// one tenant's financial data to another. Every successful login sets this claim, so under
    /// normal operation this never throws; a throw signals a genuinely broken/unauthenticated
    /// state where no tenant-scoped query should run anyway.
    /// </summary>
    public int TenantId => _tenantId
        ?? throw new InvalidOperationException(
            "No valid TenantId claim in the current context; refusing to run a tenant-scoped query. " +
            "This previously defaulted to tenant 1 and risked cross-tenant data exposure.");
}
