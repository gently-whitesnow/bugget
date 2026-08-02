using System.Text.Json;
using Bugget.Api.Mappers;
using Bugget.Api.Users.Mappers;
using Bugget.Application.ExternalSearch.Models;
using Bugget.Application.Results;
using Bugget.Domain.Analytics;
using FluentAssertions;
using DomainUsers = Bugget.Domain.Users;

namespace Bugget.UnitTests.Mappers;

/// <summary>
/// Публичный неотрицательный Int64 уходит на провод строкой канона
/// <c>Int64String</c>. Здесь проверяется не тип поля (его держит генерация из
/// контракта), а то, ради чего он менялся: значение доезжает до JSON цифра
/// в цифру.
///
/// Проверка идёт по сериализованному телу, а не по свойству DTO: между
/// маппером и клиентом стоит System.Text.Json, и «строка в DTO» ещё не значит
/// «строка на проводе».
///
/// <c>9007199254740993</c> — первое целое, которое клиент теряет в double
/// (доезжало бы как <c>...992</c>); <c>long.MaxValue</c> — верхняя граница
/// канона.
/// </summary>
public class PublicInt64WireTests
{
    private const long UnsafeForDouble = 9007199254740993L;
    private const string UnsafeWire = "9007199254740993";
    private const string MaxWire = "9223372036854775807";

    private static readonly DateTimeOffset Moment = DateTimeOffset.UnixEpoch;

    private static string Wire<T>(T contract) =>
        JsonSerializer.Serialize(contract, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact(DisplayName = "reports: total и count уходят строкой без округления")]
    public void Reports_total_and_count_survive_serialization()
    {
        var list = new ReportViews { Total = UnsafeForDouble, Reports = [] }.ToContract();
        list.Total.Should().Be(UnsafeWire);
        Wire(list).Should().Contain($"\"total\":\"{UnsafeWire}\"");

        var counts = new[]
        {
            new KeyValuePair<string, long>("beta-active", UnsafeForDouble),
            new KeyValuePair<string, long>("team-active", long.MaxValue),
        }.ToCountsContract();

        Wire(counts).Should().Contain($"\"count\":\"{UnsafeWire}\"").And.Contain($"\"count\":\"{MaxWire}\"");
    }

    [Fact(DisplayName = "external: total уходит строкой без округления")]
    public void External_total_survives_serialization()
    {
        var contract = new ExternalSearchResult { Total = UnsafeForDouble, Items = [] }.ToContract();

        Wire(contract).Should().Contain($"\"total\":\"{UnsafeWire}\"");
    }

    [Fact(DisplayName = "analytics: report_id детали уходит строкой без округления")]
    public void Analytics_report_id_survives_serialization()
    {
        var contract = new AnalyticsReportBo
        {
            ReportId = UnsafeForDouble,
            PhaseTimeline = [],
            RegressionCycles = 0,
            BugsByStatus = new BugsByStatusBo(),
            BugsAddedDuringRegression = 0,
        }.ToContract();

        Wire(contract).Should().Contain($"\"report_id\":\"{UnsafeWire}\"");
    }

    [Fact(DisplayName = "analytics: report_id в top-10 и в разрезе ответственного уходит строкой")]
    public void Analytics_lists_keep_report_id_exact()
    {
        var summary = new AnalyticsSummaryBo
        {
            Period = Period(),
            TopRegressionReports = [new TopRegressionReportBo { ReportId = UnsafeForDouble, Title = "t", RegressionCycles = 1 }],
            PhaseTrendsWeekly = [],
        }.ToContract();

        Wire(summary).Should().Contain($"\"report_id\":\"{UnsafeWire}\"");

        var responsible = new AnalyticsResponsibleBo
        {
            Period = Period(),
            ReportsParticipated =
            [
                new ResponsibleParticipatedReportBo
                {
                    ReportId = UnsafeForDouble,
                    Title = "участие",
                    CurrentPhase = (short)Bugget.Domain.Reports.ReportStatus.Test,
                },
            ],
            ReportsCompleted =
            [
                new ResponsibleCompletedReportBo
                {
                    ReportId = long.MaxValue,
                    Title = "завершён",
                    ClosedAt = Moment,
                    Outcome = (short)Bugget.Domain.Reports.ReportStatus.Resolved,
                },
            ],
        }.ToContract();

        Wire(responsible).Should()
            .Contain($"\"report_id\":\"{UnsafeWire}\"")
            .And.Contain($"\"report_id\":\"{MaxWire}\"");
    }

    [Fact(DisplayName = "users: id профиля и user_id членства уходят строкой без округления")]
    public void Users_identifiers_survive_serialization()
    {
        var profile = new DomainUsers.User
        {
            Id = UnsafeForDouble,
            ExternalId = "keycloak|1",
            Name = "Тестер",
            RegistrationDate = Moment,
            UpdatedAt = Moment,
        }.ToContract();

        Wire(profile).Should().Contain($"\"id\":\"{UnsafeWire}\"");

        var member = new DomainUsers.WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = long.MaxValue,
            Role = "admin",
            CreatedAt = Moment,
        }.ToContract();

        Wire(member).Should().Contain($"\"user_id\":\"{MaxWire}\"");
    }

    private static PeriodWindow Period() => new()
    {
        From = Moment,
        To = Moment.AddDays(7),
        Label = "7d",
    };
}
