using Bugget.Domain.Attachments;

namespace Bugget.Application.Ports;

/// <summary>
/// Пережатие вложения (картинка, видео, текст). Чем именно — дело инфраструктуры; прикладной слой
/// знает только, что получит новый объект в хранилище и его метаданные.
/// </summary>
public interface IAttachmentOptimizer
{
    /// <summary>Пережимает <paramref name="original"/> и кладёт результат в хранилище.</summary>
    /// <exception cref="InvalidOperationException">Тип содержимого пережимать нечем.</exception>
    Task<OptimizationResult> OptimizeAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream original,
        CancellationToken ct = default);
}
