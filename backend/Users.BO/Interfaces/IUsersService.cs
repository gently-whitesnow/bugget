using Bugget.Entities.Errors;
using Users.Entities.DbModels.Users;
using Users.Entities.Dto.Users;

namespace Users.BO.Interfaces;

public interface IUsersService
{
    Task<UserDbModel> TryInsertUserAsync(CreateUserDto createUserDto);
    Task<(UserContext? Value, Error? Error)> GetUserContextAsync(long userId);
    Task<(UserContext? Value, Error? Error)> GetUserContextByExternalIdAsync(string externalId);
    Task<UserDbModel?> GetUserAsync(long userId);
    Task<UserDbModel[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null);
    Task<UserDbModel[]> ListUsersAsync(long[] userIds, int? workspaceId);
    Task DeleteUserAsync(long userId);
    Task<UserDbModel> PutUserAsync(long userId, PutUserDto putUserDto);
    Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId);
    Task<(bool Success, string? ErrorCode)> MergeUsersAsync(long targetUserId, long sourceUserId);
}
