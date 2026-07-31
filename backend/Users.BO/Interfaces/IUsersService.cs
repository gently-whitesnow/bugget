using Bugget.Entities.Errors;
using Users.Entities.BO;
using Users.Entities.Dto.Users;

namespace Users.BO.Interfaces;

public interface IUsersService
{
    Task<User> TryInsertUserAsync(CreateUserDto createUserDto);
    Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long userId);
    Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId);
    Task<User?> GetUserAsync(long userId);
    Task<User[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null);
    Task<User[]> ListUsersAsync(long[] userIds, int? workspaceId);
    Task DeleteUserAsync(long userId);
    Task<User> PutUserAsync(long userId, PutUserDto putUserDto);
    Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId);
    Task<(bool Success, string? ErrorCode)> MergeUsersAsync(long targetUserId, long sourceUserId);
}
