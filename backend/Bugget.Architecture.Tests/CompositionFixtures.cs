namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Фикстура для доказательства красноты правила композиционного корня: «контроллер»,
/// который тянет тип инфраструктуры. Живёт в тестовой сборке и в продуктовый код не попадает.
/// </summary>
public sealed class LeakingController
{
    public global::Bugget.Infrastructure.AssemblyMarker Leak { get; } = new();
}

/// <summary>
/// Вторая фикстура: тип с «правильным» суффиксом <c>*Extensions</c>, которого нет в списке
/// композиционного корня. Проверяет, что правило смотрит в список, а не на имя, — именно
/// так соглашение об именовании превращалось бы в дыру.
/// </summary>
public static class ForeignServiceCollectionExtensions
{
    public static global::Bugget.Infrastructure.AssemblyMarker Leak() => new();
}

/// <summary>
/// Фикстура для доказательства красноты правила DI всей сборки Api: «хаб», который берёт
/// конкретный application-сервис. Именно такой тип проходил мимо гейта, пока правило
/// смотрело только на суффикс <c>*Controller</c>.
/// </summary>
public sealed record LeakingReportPageHub(
    global::Bugget.Application.Services.Reports.ReportsService Service);

/// <summary>
/// Второй нарушитель — без какого-либо узнаваемого суффикса. Правило смотрит на сборку
/// и конструктор, а не на имя типа.
/// </summary>
public sealed record LeakingRealtimePublisher(
    global::Bugget.Application.Services.Reports.ReportsService Service);

/// <summary>
/// Фикстура для доказательства зелёности: тип, который перечислен в композиционном корне
/// и потому вправе видеть конкретные реализации — выбор реализации и есть его работа.
/// </summary>
public sealed record WiringCompositionRoot(
    global::Bugget.Application.Services.Reports.ReportsService Service);

/// <summary>
/// Третий нарушитель правила DI всей сборки Api — форма будущего MCP-tool-класса
/// (P2b/P2c): не контроллер и не хаб, конкретный application-сервис приходит в
/// конструктор. Доказывает, что новый слой Mcp попадает под существующие правила
/// сборки, а не требует отдельного.
/// </summary>
public sealed record LeakingMcpTool(
    global::Bugget.Application.Services.Reports.ReportsService Service);
