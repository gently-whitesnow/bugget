namespace Bugget.Architecture.Tests;

/// <summary>
/// Известное отступление от целевой архитектуры: зафиксировано явно, а не замолчано.
/// </summary>
/// <param name="From">Проект или пространство имён, которое отступает.</param>
/// <param name="To">Проект, сборка или пространство имён, на которое оно ссылается вопреки целевому правилу.</param>
/// <param name="What">Что именно нарушено.</param>
/// <param name="Why">Почему это ещё живо и чем снимается.</param>
/// <param name="Adr">ADR, в котором записано целевое состояние.</param>
public sealed record Deviation(string From, string To, string What, string Why, string Adr)
{
    public override string ToString() => $"{From} → {To}: {What} (целевое состояние — {Adr}; {Why})";
}
