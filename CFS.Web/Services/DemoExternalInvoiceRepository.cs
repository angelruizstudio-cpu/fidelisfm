using System.Collections.Concurrent;
using CFS.Core.Services;

namespace CFS.Web.Services;

public sealed class DemoExternalInvoiceRepository : IExternalInvoiceRepository
{
    private readonly ConcurrentDictionary<string, int> _apiKeyHashesByTenant = new();
    private int _nextId = 1;

    public Task<TenantApiKeyLookup?> FindTenantByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_apiKeyHashesByTenant.TryGetValue(apiKeyHash, out var tenantId) ? new TenantApiKeyLookup(tenantId) : null);

    public Task<(int Id, bool AlreadyExisted)> CreateInvoiceRequestAsync(ExternalInvoiceRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult((Interlocked.Increment(ref _nextId), false));

    public Task MarkInvoiceSucceededAsync(
        int id,
        string stripeCustomerId,
        string stripeInvoiceId,
        string? stripeHostedInvoiceUrl,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MarkInvoiceFailedAsync(int id, string errorMessage, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int> CreateApiKeyAsync(int tenantId, string apiKeyHash, string? label, CancellationToken cancellationToken = default)
    {
        _apiKeyHashesByTenant[apiKeyHash] = tenantId;
        return Task.FromResult(_nextId++);
    }
}
