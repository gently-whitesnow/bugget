using Bugget.Domain.Authentication;

namespace Bugget.Domain.Common;

/// <summary>
/// Как способ аутентификации превращается в <see cref="CreatorType"/> записи в истории.
/// PAT-запрос → <see cref="CreatorType.Agent"/>; иначе (JWT / заголовки nginx) → User.
/// </summary>
public static class ActorCreatorTypes
{
    public static CreatorType FromAuthMethod(string? authMethod) =>
        string.Equals(authMethod, AuthMethods.Pat, StringComparison.Ordinal)
            ? CreatorType.Agent
            : CreatorType.User;
}
