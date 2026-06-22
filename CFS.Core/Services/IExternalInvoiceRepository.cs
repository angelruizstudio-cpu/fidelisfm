namespace CFS.Core.Services;

public sealed record TenantApiKeyLookup(int TenantId);

public sealed record ExternalInvoiceRequest(
    int TenantId,
    string RecipientName,
    string RecipientEmail,
    int AmountCents,
    string Currency,
    string Description,
    string? ExternalReference);

public interface IExternalInvoiceRepository
{
    /// <summary>
    /// Resolves a presented API key to the tenant it belongs to, or null if the key is
    /// unknown or revoked. Keys are stored hashed; callers must hash before lookup.
    /// </summary>
    Task<TenantApiKeyLookup?> FindTenantByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new invoice request and returns its id. If ExternalReference is set and
    /// already used by this tenant, returns the existing request's id instead (idempotency).
    /// </summary>
    Task<(int Id, bool AlreadyExisted)> CreateInvoiceRequestAsync(ExternalInvoiceRequest request, CancellationToken cancellationToken = default);

    Task MarkInvoiceSucceededAsync(
        int id,
        string stripeCustomerId,
        string stripeInvoiceId,
        string? stripeHostedInvoiceUrl,
        CancellationToken cancellationToken = default);

    Task MarkInvoiceFailedAsync(int id, string errorMessage, CancellationToken cancellationToken = default);

    Task<int> CreateApiKeyAsync(int tenantId, string apiKeyHash, string? label, CancellationToken cancellationToken = default);
}
