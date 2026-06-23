namespace CFS.Core.Models;

public sealed record SalesLead(
    string OrganizationName,
    string ContactName,
    string Email,
    string Phone,
    int ChurchCount,
    IReadOnlyList<string> KeyFeatures,
    string Timeline,
    string? Comments);
