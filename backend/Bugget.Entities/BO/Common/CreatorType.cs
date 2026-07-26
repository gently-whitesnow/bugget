namespace Bugget.Entities.BO.Common;

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
    TgBetaTester = 2
}
