namespace Bugget.Application.Services.Attachments;

/// <summary>
/// Шов между отменяемой подготовкой вложения и неотменяемой записью результата. Токен очереди останавливает
/// ожидание слота и ffmpeg при остановке приложения (MAIN-240), но начатая цепочка «storage → БД → уведомление →
/// удаление temp» обязана довыполниться: иначе файл записан, а строка в БД указывает на удалённый temp-ключ.
/// </summary>
public static class AttachmentPersistence
{
    /// <summary>Последняя точка отмены фоновой оптимизации: дальше только изменяющие вызовы, токен уже не отменяется.</summary>
    public static CancellationToken BeginPersisting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return CancellationToken.None;
    }

    /// <summary>Продолжение уже начатой записи: точка невозврата пройдена выше по стеку.</summary>
    public static CancellationToken Persisting => CancellationToken.None;
}
