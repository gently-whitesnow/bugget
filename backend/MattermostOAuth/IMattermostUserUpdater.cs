namespace MattermostOAuth;

public interface IMattermostUserUpdater
{
    Task UpdateMattermostUserIdAsync(long userId, string mattermostUserId);
}
