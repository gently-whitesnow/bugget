using Bugget.Domain.Attachments;

namespace Bugget.Application.Ports;

/// <summary>
/// Пережатие вложения: картинку — в компактный формат с превью, видео — в меньший
/// битрейт, текст — в архив. Что именно и какой библиотекой — дело инфраструктуры;
/// прикладной слой знает только, что на выходе получит новый объект в хранилище
/// и его метаданные.
/// </summary>
public interface IAttachmentOptimizer
{
    /// <summary>
    /// Пережимает содержимое <paramref name="original"/> и кладёт результат в хранилище.
    /// </summary>
    /// <param name="organizationId">Рабочая область — часть ключа в хранилище.</param>
    /// <param name="reportId">Репорт, которому принадлежит вложение.</param>
    /// <param name="attachment">Вложение: из него берутся имя файла и mime.</param>
    /// <param name="original">Содержимое исходного файла.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <exception cref="InvalidOperationException">Тип содержимого пережимать нечем.</exception>
    Task<OptimizationResult> OptimizeAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream original,
        CancellationToken ct = default);
}
