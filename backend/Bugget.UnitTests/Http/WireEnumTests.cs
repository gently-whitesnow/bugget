using System.Text.Json;
using Bugget.Api.Extensions;
using Bugget.Contracts.Reports.Generated;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bugget.UnitTests.Http;

/// <summary>
/// Enum'ы модуля reports уходят на провод строкой из <c>enum</c> контракта.
///
/// Проверять это обязательно: генератор вешает на свойства собственный
/// <c>JsonStringEnumConverter&lt;T&gt;</c>, который пишет имя CLR-члена, а имена
/// он получает по своим правилам (<c>tg_beta_tester</c> → <c>Tg_beta_tester</c>).
/// Совпадение имени со строкой контракта — совпадение, а не правило, и на первом
/// же многословном значении оно ломается.
///
/// Опции берутся из настоящей настройки пайплайна, а не собираются рядом: тест
/// про провод, а не про конвертер в вакууме.
/// </summary>
public sealed class WireEnumTests
{
    private static readonly JsonSerializerOptions Options = new ServiceCollection()
        .AddMvcPipeline()
        .BuildServiceProvider()
        .GetRequiredService<IOptions<JsonOptions>>()
        .Value
        .JsonSerializerOptions;

    [Theory(DisplayName = "Статус репорта отдаётся строкой контракта")]
    [InlineData(ReportStatus.Backlog, "backlog")]
    [InlineData(ReportStatus.Resolved, "resolved")]
    [InlineData(ReportStatus.Fix, "fix")]
    [InlineData(ReportStatus.Rejected, "rejected")]
    [InlineData(ReportStatus.Test, "test")]
    public void ReportStatusIsWrittenAsWireString(ReportStatus status, string wire)
    {
        var json = JsonSerializer.Serialize(new ReportPatchResult { Status = status }, Options);

        json.Should().Contain($"\"status\":\"{wire}\"");
    }

    [Theory(DisplayName = "Тип автора отдаётся строкой контракта, включая многословную")]
    [InlineData(CreatorType.User, "user")]
    [InlineData(CreatorType.System, "system")]
    [InlineData(CreatorType.Tg_beta_tester, "tg_beta_tester")]
    [InlineData(CreatorType.Agent, "agent")]
    public void CreatorTypeIsWrittenAsWireString(CreatorType creatorType, string wire)
    {
        var json = JsonSerializer.Serialize(new CommentSummary { Creator_type = creatorType }, Options);

        json.Should().Contain($"\"creator_type\":\"{wire}\"");
    }

    [Theory(DisplayName = "Тип вложения отдаётся строкой контракта")]
    [InlineData(AttachType.Fact, "fact")]
    [InlineData(AttachType.Expected, "expected")]
    [InlineData(AttachType.Comment, "comment")]
    [InlineData(AttachType.Bug_step, "bug_step")]
    public void AttachTypeIsWrittenAsWireString(AttachType attachType, string wire)
    {
        var json = JsonSerializer.Serialize(new AttachmentSummary { Attach_type = attachType }, Options);

        json.Should().Contain($"\"attach_type\":\"{wire}\"");
    }

    [Theory(DisplayName = "Аудитория и статус бага читаются из строки контракта")]
    [InlineData("internal", CommentAudience.Internal)]
    [InlineData("external", CommentAudience.External)]
    public void CommentAudienceIsReadFromWireString(string wire, CommentAudience expected)
    {
        var request = JsonSerializer.Deserialize<CommentRequest>(
            $"{{\"text\":\"текст\",\"audience\":\"{wire}\"}}", Options);

        request!.Audience.Should().Be(expected);
    }

    [Theory(DisplayName = "Статус бага читается из строки контракта")]
    [InlineData("open", BugStatus.Open)]
    [InlineData("verified", BugStatus.Verified)]
    [InlineData("rejected", BugStatus.Rejected)]
    [InlineData("fixed", BugStatus.Fixed)]
    public void BugStatusIsReadFromWireString(string wire, BugStatus expected)
    {
        var request = JsonSerializer.Deserialize<BugPatchRequest>($"{{\"status\":\"{wire}\"}}", Options);

        request!.Status.Should().Be(expected);
    }

    /// <summary>
    /// Разбор строгий: ни имя CLR-члена, ни другой регистр, ни число старого
    /// провода значением контракта не являются.
    /// </summary>
    [Theory(DisplayName = "Значение вне контракта отвергается разбором")]
    [InlineData("\"Open\"")]
    [InlineData("\"OPEN\"")]
    [InlineData("\"unknown\"")]
    [InlineData("0")]
    public void UnknownWireValueIsRejected(string raw)
    {
        var read = () => JsonSerializer.Deserialize<BugPatchRequest>($"{{\"status\":{raw}}}", Options);

        read.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Пропущенный и явный null у optional-поля остаются null")]
    public void OptionalEnumKeepsNullSemantics()
    {
        JsonSerializer.Deserialize<BugPatchRequest>("{}", Options)!.Status.Should().BeNull();
        JsonSerializer.Deserialize<BugPatchRequest>("{\"status\":null}", Options)!.Status.Should().BeNull();

        var json = JsonSerializer.Serialize(new BugPatchRequest { Status = null }, Options);
        json.Should().Contain("\"status\":null");
    }

    /// <summary>
    /// У элементов массива генератор конвертер не проставляет — их накрывает
    /// глобальная фабрика. Фильтры счётчиков — единственное место, где enum
    /// приходит в теле массивом.
    /// </summary>
    [Fact(DisplayName = "Массив фильтров читается и пишется строками контракта")]
    public void EnumArraysUseWireStrings()
    {
        var scope = JsonSerializer.Deserialize<ReportCountsScope>(
            "{\"key\":\"k\",\"statuses\":[\"backlog\",\"fix\"],\"creator_types\":[\"tg_beta_tester\"]}",
            Options);

        scope!.Statuses.Should().Equal(ReportStatus.Backlog, ReportStatus.Fix);
        scope.Creator_types.Should().Equal(CreatorType.Tg_beta_tester);

        JsonSerializer.Serialize(scope, Options)
            .Should().Contain("\"statuses\":[\"backlog\",\"fix\"]");
    }

    [Fact(DisplayName = "Пустой массив фильтров остаётся пустым, а не превращается в null")]
    public void EmptyEnumArrayIsPreserved()
    {
        var scope = JsonSerializer.Deserialize<ReportCountsScope>(
            "{\"key\":\"k\",\"statuses\":[]}", Options);

        scope!.Statuses.Should().BeEmpty();
        scope.Creator_types.Should().BeNull();
    }
}
