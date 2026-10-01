namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Фикстура для доказательства красноты правила DI всей сборки Api: «хаб», который берёт
/// конкретный application-сервис. Именно такой тип проходил мимо гейта, пока правило
/// смотрело только на суффикс <c>*Controller</c>.
/// </summary>
public sealed record LeakingReportPageHub(
    global::Bugget.Application.Services.Reports.ReportsService Service);
