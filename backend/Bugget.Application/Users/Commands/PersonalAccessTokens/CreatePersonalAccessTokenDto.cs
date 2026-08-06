using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.PersonalAccessTokens;

public sealed class CreatePersonalAccessTokenDto
{
    public required long UserId { get; init; }
    public required int WorkspaceId { get; init; }
    public required int TeamId { get; init; }

    [StringLength(128, MinimumLength = 1)]
    public required string Label { get; init; }

    /// <summary>SHA-256 полного значения токена: само значение в БД не уходит.</summary>
    public required byte[] TokenHash { get; init; }

    [StringLength(32, MinimumLength = 1)]
    public required string TokenPrefix { get; init; }

    /// <summary><c>null</c> — бессрочный токен, см. <see cref="Domain.Users.PersonalAccessToken.ResolveExpiresAt"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
