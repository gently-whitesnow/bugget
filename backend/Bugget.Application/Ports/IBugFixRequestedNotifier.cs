namespace Bugget.Application.Ports;

/// <summary>
/// Сигнал раннеру агента: пользователь попросил починить баг. Куда и как сигнал
/// уходит — дело адаптера; штатная реализация — HTTP-вебхук с глобальной
/// env-конфигурацией и no-op, пока раннер не настроен.
/// </summary>
public interface IBugFixRequestedNotifier
{
    Task NotifyAsync(BugFixRequestedPayload payload, CancellationToken cancellationToken);
}

/// <summary>
/// Что уходит раннеру: только идентификаторы и внешний путь репорта — никаких
/// секретов. PAT для работы с bugget раннер держит у себя заранее.
/// </summary>
public sealed record BugFixRequestedPayload(
    string? WorkspaceId,
    string? TeamId,
    string ReportId,
    int BugId,
    string RequestedByUserId,
    string ReportPath);
