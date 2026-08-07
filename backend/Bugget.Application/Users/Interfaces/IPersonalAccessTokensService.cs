using Bugget.Application.Users.Results.PersonalAccessTokens;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.Interfaces;

public interface IPersonalAccessTokensService
{
    /// <summary>
    /// Выпускает токен и возвращает его значение единственный раз: дальше существует
    /// только хэш, и показать значение повторно нечем.
    /// </summary>
    Task<IssuedPersonalAccessToken> IssueAsync(
        long userId,
        int workspaceId,
        int teamId,
        string label,
        DateTimeOffset? requestedExpiresAt);

    Task<PersonalAccessToken[]> ListAsync(long userId);

    /// <summary><c>false</c> — токена нет, он чужой или уже отозван.</summary>
    Task<bool> RevokeAsync(long tokenId, long userId);
}
