namespace Bugget.Application.Ports;

/// <summary>
/// Определение типа содержимого по самим байтам. Заголовку клиента верить нельзя:
/// он приходит снаружи, а по типу принимается решение о допустимости вложения.
/// </summary>
public interface IMimeTypeDetector
{
    /// <summary>
    /// Читает начало потока и возвращает mime-тип; поток перематывается назад,
    /// если это возможно. Когда тип определить не удалось — <c>application/octet-stream</c>.
    /// </summary>
    Task<string> DetectAsync(Stream content, CancellationToken ct = default);
}
