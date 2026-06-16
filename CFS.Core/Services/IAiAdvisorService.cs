using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IAiAdvisorService
{
    Task<AiAnswer> AskAsync(
        AiQuestionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDynamicInsightService
{
    Task<IReadOnlyList<DynamicInsight>> GetReportInsightsAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default);
}
