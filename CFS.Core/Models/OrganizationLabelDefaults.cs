namespace CFS.Core.Models;

/// <summary>
/// Default Spanish UI labels per organization type. Used as the baseline whenever a tenant
/// has no custom OrganizationSettings row, or to fill in any unset custom label fields.
/// </summary>
public static class OrganizationLabelDefaults
{
    public static OrganizationLabels GetDefaults(OrganizationType type) => type switch
    {
        OrganizationType.Church => new OrganizationLabels(
            ContactsLabel: "Miembros",
            IncomeLabel: "Diezmos y Ofrendas",
            ContributionsLabel: "Contribuciones",
            DepartmentsLabel: "Ministerios",
            FundsLabel: "Fondos",
            ReportsLabel: "Reportes",
            LeadershipReportLabel: "Reporte Pastoral / Tesorería",
            PrimaryOfficerLabel: "Pastor",
            SecondaryOfficerLabel: "Tesorería"),

        OrganizationType.EducationalInstitution => new OrganizationLabels(
            ContactsLabel: "Estudiantes / Donantes",
            IncomeLabel: "Pagos y Donaciones",
            ContributionsLabel: "Pagos / Aportaciones",
            DepartmentsLabel: "Programas Académicos",
            FundsLabel: "Fondos / Becas",
            ReportsLabel: "Reportes Administrativos",
            LeadershipReportLabel: "Reporte Administrativo",
            PrimaryOfficerLabel: "Director",
            SecondaryOfficerLabel: "Finanzas"),

        OrganizationType.Nonprofit => new OrganizationLabels(
            ContactsLabel: "Contactos / Donantes",
            IncomeLabel: "Aportaciones / Donaciones",
            ContributionsLabel: "Donaciones",
            DepartmentsLabel: "Programas",
            FundsLabel: "Fondos Restringidos",
            ReportsLabel: "Reportes",
            LeadershipReportLabel: "Reporte para Junta",
            PrimaryOfficerLabel: "Director Ejecutivo",
            SecondaryOfficerLabel: "Tesorería"),

        OrganizationType.Ministry => new OrganizationLabels(
            ContactsLabel: "Contactos / Donantes",
            IncomeLabel: "Donaciones / Aportaciones",
            ContributionsLabel: "Contribuciones",
            DepartmentsLabel: "Áreas Ministeriales",
            FundsLabel: "Fondos",
            ReportsLabel: "Reportes",
            LeadershipReportLabel: "Reporte Ministerial",
            PrimaryOfficerLabel: "Director",
            SecondaryOfficerLabel: "Tesorería"),

        OrganizationType.Foundation => new OrganizationLabels(
            ContactsLabel: "Contactos / Donantes",
            IncomeLabel: "Donaciones / Grants",
            ContributionsLabel: "Aportaciones",
            DepartmentsLabel: "Programas",
            FundsLabel: "Fondos / Grants",
            ReportsLabel: "Reportes",
            LeadershipReportLabel: "Reporte para Junta",
            PrimaryOfficerLabel: "Director Ejecutivo",
            SecondaryOfficerLabel: "Finanzas"),

        OrganizationType.CommunityOrganization => new OrganizationLabels(
            ContactsLabel: "Contactos / Participantes",
            IncomeLabel: "Aportaciones / Fondos",
            ContributionsLabel: "Contribuciones",
            DepartmentsLabel: "Programas Comunitarios",
            FundsLabel: "Fondos / Proyectos",
            ReportsLabel: "Reportes",
            LeadershipReportLabel: "Reporte Administrativo",
            PrimaryOfficerLabel: "Director",
            SecondaryOfficerLabel: "Finanzas"),

        // Other uses the same defaults as Nonprofit.
        OrganizationType.Other => GetDefaults(OrganizationType.Nonprofit),

        _ => GetDefaults(OrganizationType.Church)
    };
}
