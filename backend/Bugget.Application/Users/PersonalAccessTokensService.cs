using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Bugget.Application.Users.Results.PersonalAccessTokens;
using Bugget.Domain.Users;

namespace Bugget.Application.Users;

public sealed class PersonalAccessTokensService(
    IPersonalAccessTokensDbClient tokensDbClient) : IPersonalAccessTokensService
{
    public async Task<IssuedPersonalAccessToken> IssueAsync(
        long userId,
        int workspaceId,
        int teamId,
        string label,
        DateTimeOffset? requestedExpiresAt)
    {
        var secret = PersonalAccessTokenSecret.Generate();
        var token = await tokensDbClient.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            Label = label,
            TokenHash = PersonalAccessTokenSecret.ComputeHash(secret.Value),
            TokenPrefix = secret.DisplayPrefix,
            ExpiresAt = PersonalAccessToken.ResolveExpiresAt(DateTimeOffset.UtcNow, requestedExpiresAt)
        });

        return new IssuedPersonalAccessToken(token, secret.Value);
    }

    public Task<PersonalAccessToken[]> ListAsync(long userId)
    {
        return tokensDbClient.ListAsync(userId);
    }

    public Task<bool> RevokeAsync(long tokenId, long userId)
    {
        return tokensDbClient.RevokeAsync(tokenId, userId);
    }
}
