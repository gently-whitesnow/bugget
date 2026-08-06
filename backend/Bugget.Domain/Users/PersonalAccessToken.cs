namespace Bugget.Domain.Users;

/// <summary>
/// Токен неинтерактивного доступа к API: выпускается пользователем себе, привязан к одной
/// паре workspace+team и заменяет собой OIDC-сессию для скриптов и MCP.
/// Секрет здесь не хранится — в БД лежит только его хэш (<see cref="PersonalAccessTokenSecret"/>),
/// и функции users_db его не возвращают, поэтому в модель он попасть не может.
/// </summary>
public sealed class PersonalAccessToken
{
    /// <summary>
    /// Сколько живёт токен, если срок не задан явно при выпуске. Бессрочный токен —
    /// осознанное действие пользователя, а не то, что получается само собой.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(90);

    public required long Id { get; init; }
    public required long UserId { get; init; }
    public required int WorkspaceId { get; init; }
    public required int TeamId { get; init; }
    public required string Label { get; init; }

    /// <summary>
    /// Открытое начало секрета. Нужно, чтобы пользователь узнал свой токен в списке:
    /// полное значение он видел один раз при выпуске.
    /// </summary>
    public required string TokenPrefix { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    /// <summary>
    /// Годен ли токен для аутентификации. Проверку области (workspace+team) делает
    /// вызывающий: она сравнивается с контекстом запроса, а не с самим токеном.
    /// </summary>
    public bool IsUsable(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Срок для выпускаемого токена: запрошенный, иначе умолчание. Бессрочный токен
    /// получается не отсюда — вызывающий кладёт в команду выпуска <c>null</c> напрямую,
    /// и это видно в его коде.
    /// </summary>
    public static DateTimeOffset ResolveExpiresAt(DateTimeOffset now, DateTimeOffset? requested) =>
        requested ?? now.Add(DefaultLifetime);
}
