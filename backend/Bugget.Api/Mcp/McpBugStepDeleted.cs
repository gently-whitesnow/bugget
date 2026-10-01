namespace Bugget.Api.Mcp;

/// <summary>
/// Ответ <c>delete_bug_step</c>: подтверждение с id удалённого шага. Номера
/// оставшихся шагов не пересчитываются (DELETE без перенумерации) — актуальное
/// состояние модель берёт из <c>get_report</c>, а не из этого ответа.
/// </summary>
internal sealed record McpBugStepDeleted(int DeletedStepId);
