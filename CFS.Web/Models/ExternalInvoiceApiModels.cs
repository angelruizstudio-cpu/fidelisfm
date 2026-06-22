namespace CFS.Web.Models;

public sealed record CreateInvoiceApiRequest(
    string RecipientName,
    string RecipientEmail,
    decimal Amount,
    string? Currency,
    string Description,
    string? ExternalReference);

public sealed record CreateInvoiceApiResponse(
    int RequestId,
    string Status,
    string? StripeInvoiceId,
    string? HostedInvoiceUrl,
    string? ErrorMessage);

public sealed record CreateApiKeyRequest(string? Label);

public sealed record CreateApiKeyResponse(string ApiKey);
