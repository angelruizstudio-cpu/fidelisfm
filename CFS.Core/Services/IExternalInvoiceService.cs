namespace CFS.Core.Services;

public sealed record CreateExternalInvoiceRequest(
    int TenantId,
    string RecipientName,
    string RecipientEmail,
    int AmountCents,
    string Currency,
    string Description,
    string? ExternalReference);

public sealed record CreateExternalInvoiceResult(
    int RequestId,
    string Status,
    string? StripeInvoiceId,
    string? HostedInvoiceUrl,
    string? ErrorMessage);

public interface IExternalInvoiceService
{
    /// <summary>
    /// Creates (or reuses, for a repeated ExternalReference) a Stripe customer and invoice
    /// for a third-party recipient on behalf of a tenant, and emails it via Stripe.
    /// </summary>
    Task<CreateExternalInvoiceResult> CreateAndSendInvoiceAsync(CreateExternalInvoiceRequest request, CancellationToken cancellationToken = default);
}
