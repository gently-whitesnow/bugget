using Bugget.Domain.Reports;

namespace Bugget.IntegrationTests;

internal sealed record SeedInterval(
    ReportStatus Phase,
    DateTimeOffset EnteredAt,
    DateTimeOffset? ExitedAt,
    int RegressionCycleIndex);
