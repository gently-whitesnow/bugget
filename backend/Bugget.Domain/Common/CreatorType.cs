namespace Bugget.Domain.Common;

public enum CreatorType
{
    /// <summary>
    /// Пользователь
    /// </summary>
    User = 0,

    /// <summary>
    /// Системный комментарий (логгирование действий)
    /// </summary>
    System = 1,

    /// <summary>
    /// Внешний автор через beta-test bot (Telegram)
    /// </summary>
    TgBetaTester = 2,

    /// <summary>
    /// Неинтерактивный клиент: запрос пришёл через PAT, а не браузерную JWT-сессию.
    /// </summary>
    Agent = 3
}
