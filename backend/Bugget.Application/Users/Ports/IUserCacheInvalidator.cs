namespace Bugget.Application.Users.Ports;

/// <summary>
/// Сброс закэшированного контекста пользователя. Не persistence: реализация
/// (<c>AuthorizationCacheAdapter</c>) чистит in-process кэш модуля authorization
/// и в БД не ходит — поэтому имя по возможности, а не по семейству <c>*DbClient</c>.
/// </summary>
public interface IUserCacheInvalidator
{
    public Task InvalidateUserCacheAsync(long userId);
}
