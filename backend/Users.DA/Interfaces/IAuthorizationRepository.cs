namespace Users.DA.Interfaces;

public interface IAuthorizationRepository
{
    public Task InvalidateUserCacheAsync(long userId);
}
