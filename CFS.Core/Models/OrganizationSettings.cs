namespace CFS.Core.Models;

public enum OrganizationType
{
    Church,
    Ministry,
    Nonprofit,
    EducationalInstitution,
    Foundation,
    CommunityOrganization,
    Other
}

public sealed record OrganizationLabels(
    string ContactsLabel,
    string IncomeLabel,
    string ContributionsLabel,
    string DepartmentsLabel,
    string FundsLabel,
    string ReportsLabel,
    string LeadershipReportLabel,
    string PrimaryOfficerLabel,
    string SecondaryOfficerLabel);

public sealed record OrganizationSettings(
    int TenantId,
    OrganizationType OrganizationType,
    string? DisplayName,
    OrganizationLabels Labels,
    DateTime CreatedAt,
    DateTime UpdatedAt);
