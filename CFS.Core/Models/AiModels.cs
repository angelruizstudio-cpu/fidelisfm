namespace CFS.Core.Models;

public sealed record AiQuestionRequest(
    int TenantId,
    int UserId,
    string Question,
    DateTime? StartDate,
    DateTime? EndDate);

public sealed record AiCitation(
    string Source,
    string Label,
    string? Reference);

public sealed record AiAnswer(
    string Answer,
    IReadOnlyList<AiCitation> Citations,
    IReadOnlyList<string> FollowUpQuestions);

public sealed record DynamicInsight(
    string Key,
    string Title,
    string Summary,
    string RootCause,
    string Severity,
    string FeatureRequired,
    decimal? ImpactAmount,
    decimal? ChangePercent);
