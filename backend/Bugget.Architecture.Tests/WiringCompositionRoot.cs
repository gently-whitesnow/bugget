namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Фикстура для доказательства зелёности: тип, который перечислен в композиционном корне
/// и потому вправе видеть конкретные реализации — выбор реализации и есть его работа.
/// </summary>
public sealed record WiringCompositionRoot(
    global::Bugget.Application.Services.Reports.ReportsService Service);
