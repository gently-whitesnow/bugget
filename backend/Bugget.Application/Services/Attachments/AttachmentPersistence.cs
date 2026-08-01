namespace Bugget.Application.Services.Attachments;

/// <summary>
/// Шов между отменяемой подготовкой вложения и неотменяемой записью результата.
/// Runtime-токен очереди обязан останавливать ожидание слота и активный ffmpeg при
/// остановке приложения (MAIN-240), но с началом записи в постоянное хранилище
/// последовательность «storage → БД → уведомление → удаление temp» обязана
/// довыполниться: оборванная посередине, она оставляет вложение в промежуточном
/// состоянии — файл записан, а строка в БД всё ещё указывает на удалённый temp-ключ.
/// </summary>
public static class AttachmentPersistence
{
    /// <summary>
    /// Последняя точка, где фоновую оптимизацию ещё можно отменить. Дальше идут только
    /// изменяющие вызовы, поэтому наружу отдаётся токен, который уже не отменяется.
    /// </summary>
    public static CancellationToken BeginPersisting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return CancellationToken.None;
    }

    /// <summary>Продолжение уже начатой записи: точка невозврата пройдена выше по стеку.</summary>
    public static CancellationToken Persisting => CancellationToken.None;
}
