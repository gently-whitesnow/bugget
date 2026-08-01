namespace Bugget.Domain.Common;

public enum CommentAudience
{
    /// <summary>
    /// Внутренний комментарий команды (legacy default).
    /// </summary>
    Internal = 0,

    /// <summary>
    /// Внешний комментарий, видимый внешнему автору (например, через beta-test bot).
    /// </summary>
    External = 1
}
