namespace Bugget.Api.Authorization.Abstractions;

public interface IExternalUser
{
    /// <summary>
    /// Идентификатор пользователя из внешней системы (обязательный)
    /// </summary>
    string ExternalId { get; }

    /// <summary>
    /// Имя пользователя (опционально — OIDC провайдеры могут не предоставлять)
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// URL изображения из внешней системы (опционально)
    /// </summary>
    string? ImageUrl { get; }
}
