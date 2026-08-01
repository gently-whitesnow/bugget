namespace Bugget.Application.Users.Ports;

public interface IAuthorizationRepository
{
    public Task InvalidateUserCacheAsync(long userId);
}
