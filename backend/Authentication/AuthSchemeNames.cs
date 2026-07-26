namespace Authentication;

public static class AuthSchemeNames
{
    /// <summary>
    /// Схема аутентификации по заголовкам Auth-Request-* для модуля users.
    /// Имя отличается от схемы модуля reports ("headers"): оба модуля живут в одном
    /// процессе объединённого bugget-api, а наборы клеймов у них разные.
    /// </summary>
    public const string Headers = "users-headers";
}
