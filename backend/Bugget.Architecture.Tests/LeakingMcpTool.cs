namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Третий нарушитель правила DI всей сборки Api — форма будущего MCP-tool-класса
/// (P2b/P2c): не контроллер и не хаб, конкретный application-сервис приходит в
/// конструктор. Доказывает, что новый слой Mcp попадает под существующие правила
/// сборки, а не требует отдельного.
/// </summary>
public sealed record LeakingMcpTool(
    global::Bugget.Application.Services.Reports.ReportsService Service);
