namespace Bugget.ExternalClients.Kaiten;

/// <summary>
/// Константы для работы с Kaiten
/// </summary>
public static class KaitenConstants
{
    /// <summary>
    /// Ключ секции настроек Kaiten
    /// </summary>
    public const string FeatureKey = "kaiten";

    /// <summary>
    /// Ключ настройки домена
    /// </summary>
    public const string DomainFieldKey = "domain";

    /// <summary>
    /// Ключ настройки токена доступа
    /// </summary>
    public const string AccessTokenFieldKey = "access_token";

    /// <summary>
    /// Ключ настройки ID досок команды
    /// </summary>
    public const string BoardIdsFieldKey = "board_ids";

    /// <summary>
    /// Ключ настройки прикрепления ссылки на репорт к карточке
    /// </summary>
    public const string UseReportLinkingFieldKey = "use_report_linking";

    /// <summary>
    /// Ключ настройки отправки ссылки на репорт в комментарии
    /// </summary>
    public const string SendReportLinkToCommentsFieldKey = "send_report_link_to_comments";
}

