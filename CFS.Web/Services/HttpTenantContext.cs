using CFS.Core.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CFS.Web.Services;

public sealed class HttpTenantContext : ITenantContext
{
    public int TenantId { get; }

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        var claim = accessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
        TenantId = int.TryParse(claim, out var id) ? id : 1;
    }
}
