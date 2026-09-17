namespace Bugget.Domain.Users;

/// <summary>
/// Токен неинтерактивного доступа: привязан к одной паре workspace+team, заменяет OIDC-сессию для скриптов и MCP.
/// Секрет не хранится — в БД только хэш (<see cref="PersonalAccessTokenSecret"/>), users_db его не возвращает.
/// </summary>
public sealed class PersonalAccessToken
{
    // Бессрочный токен — осознанное действие пользователя, а не то, что получается само собой.
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(90);

    public required long Id { get; init; }
    public required long UserId { get; init; }
    public required int WorkspaceId { get; init; }
    public required int TeamId { get; init; }
    public required string Label { get; init; }

    // Открытое начало секрета: по нему пользователь узнаёт токен в списке.
    public required string TokenPrefix { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    // Область (workspace+team) проверяет вызывающий: она сравнивается с контекстом запроса.
    public bool IsUsable(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    // Бессрочный токен получается не отсюда: вызывающий явно кладёт null в команду выпуска.
    public static DateTimeOffset ResolveExpiresAt(DateTimeOffset now, DateTimeOffset? requested) =>
        requested ?? now.Add(DefaultLifetime);
}
