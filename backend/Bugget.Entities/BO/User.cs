
namespace Bugget.Entities.BO;

public class User
{
    /// <summary>
    /// Уникальный идентификатор соответствующий хэдеру авторизации USER_ID_KEY
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Имя пользователя
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Идентификатор пользователя в системе уведомлений
    /// </summary>
    public string? MattermostUserId { get; init; }

    /// <summary>
    /// Идентификатор команды/отдела
    /// </summary>
    public string? TeamId { get; init; }

    /// <summary>
    /// Идентификатор организации
    /// </summary>
    public string? OrganizationId { get; init; }

    /// <summary>
    /// Ссылка на фотографию пользователя
    /// </summary>
    public string? ImageUrl { get; init; }
}
