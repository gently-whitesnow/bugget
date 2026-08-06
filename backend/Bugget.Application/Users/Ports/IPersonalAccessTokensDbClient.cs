using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface IPersonalAccessTokensDbClient
{
    Task<PersonalAccessToken> CreateAsync(CreatePersonalAccessTokenDto createDto);

    /// <summary>
    /// Неотозванные токены пользователя по всем его командам, свежие сверху. Просроченные
    /// в списке остаются: пользователь должен видеть, что именно истекло, и убрать это сам.
    /// </summary>
    Task<PersonalAccessToken[]> ListAsync(long userId);

    /// <summary>
    /// Поиск по хэшу секрета для аутентификации. Возвращает и просроченный, и отозванный:
    /// пригодность решает <see cref="PersonalAccessToken.IsUsable"/>.
    /// </summary>
    Task<PersonalAccessToken?> FindByHashAsync(byte[] tokenHash);

    /// <summary>
    /// Отзывает токен пользователя. <c>false</c> — токена нет, он чужой или уже отозван.
    /// </summary>
    Task<bool> RevokeAsync(long id, long userId);

    Task TouchLastUsedAsync(long id);
}
