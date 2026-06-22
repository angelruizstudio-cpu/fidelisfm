using CFS.Core.Services;
using Stripe;

namespace CFS.Web.Services;

public sealed class ExternalInvoiceService(
    IConfiguration configuration,
    IExternalInvoiceRepository repository,
    ILogger<ExternalInvoiceService> logger) : IExternalInvoiceService
{
    public async Task<CreateExternalInvoiceResult> CreateAndSendInvoiceAsync(
        CreateExternalInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var (id, alreadyExisted) = await repository.CreateInvoiceRequestAsync(
            new ExternalInvoiceRequest(
                request.TenantId,
                request.RecipientName,
                request.RecipientEmail,
                request.AmountCents,
                request.Currency,
                request.Description,
                request.ExternalReference),
            cancellationToken);

        if (alreadyExisted)
        {
            return new CreateExternalInvoiceResult(id, "AlreadyExists", null, null, null);
        }

        var secretKey = configuration["STRIPE_SECRET_KEY"]
            ?? throw new InvalidOperationException("STRIPE_SECRET_KEY is not configured.");
        var client = new StripeClient(secretKey);

        try
        {
            var customerService = new CustomerService(client);
            var customers = await customerService.ListAsync(new CustomerListOptions
            {
                Email = request.RecipientEmail,
                Limit = 1,
            }, cancellationToken: cancellationToken);

            var customer = customers.Data.FirstOrDefault() ?? await customerService.CreateAsync(new CustomerCreateOptions
            {
                Name = request.RecipientName,
                Email = request.RecipientEmail,
            }, cancellationToken: cancellationToken);

            var invoiceItemService = new InvoiceItemService(client);
            await invoiceItemService.CreateAsync(new InvoiceItemCreateOptions
            {
                Customer = customer.Id,
                Amount = request.AmountCents,
                Currency = request.Currency,
                Description = request.Description,
            }, cancellationToken: cancellationToken);

            var invoiceService = new InvoiceService(client);
            var invoice = await invoiceService.CreateAsync(new InvoiceCreateOptions
            {
                Customer = customer.Id,
                CollectionMethod = "send_invoice",
                DaysUntilDue = 30,
                AutoAdvance = true,
                Metadata = new Dictionary<string, string>
                {
                    ["TenantId"] = request.TenantId.ToString(),
                    ["ExternalInvoiceRequestId"] = id.ToString(),
                },
            }, cancellationToken: cancellationToken);

            var finalizedInvoice = await invoiceService.FinalizeInvoiceAsync(invoice.Id, cancellationToken: cancellationToken);
            var sentInvoice = await invoiceService.SendInvoiceAsync(finalizedInvoice.Id, cancellationToken: cancellationToken);

            await repository.MarkInvoiceSucceededAsync(id, customer.Id, sentInvoice.Id, sentInvoice.HostedInvoiceUrl, cancellationToken);

            return new CreateExternalInvoiceResult(id, "Sent", sentInvoice.Id, sentInvoice.HostedInvoiceUrl, null);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Failed to create/send Stripe invoice for external invoice request {RequestId}.", id);
            await repository.MarkInvoiceFailedAsync(id, ex.Message, cancellationToken);
            return new CreateExternalInvoiceResult(id, "Failed", null, null, ex.Message);
        }
    }
}
