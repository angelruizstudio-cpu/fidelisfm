using CFS.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CFS.Web.Services;

public sealed class HttpTenantContext : ITenantContext
{
    public int TenantId { get; }

    public HttpTenantContext(AuthenticationStateProvider authenticationStateProvider)
    {
        // In Blazor Server, IHttpContextAccessor.HttpContext is only reliable during the
        // initial prerender — once the interactive SignalR circuit takes over it can be
        // null, silently defaulting every tenant-scoped query to tenant 1. The
        // AuthenticationStateProvider cascade is the mechanism Blazor Server guarantees to
        // work across the whole circuit lifetime, so resolve the tenant claim from there.
        var authState = authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
        var claim = authState.User.FindFirst("TenantId")?.Value;
        TenantId = int.TryParse(claim, out var id) ? id : 1;
    }
}
