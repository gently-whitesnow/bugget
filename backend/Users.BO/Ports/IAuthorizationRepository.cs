namespace Users.BO.Ports;

public interface IAuthorizationRepository
{
    public Task InvalidateUserCacheAsync(long userId);
}
