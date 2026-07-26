using Flow;
using Users.Entities.DbModels.Users;
using Users.Entities.Dto.Users;

namespace Users.BO.Interfaces;

public interface IUsersService
{
    Task<UserDbModel> TryInsertUserAsync(CreateUserDto createUserDto);
    Task<ResultStruct<UserContext?>> GetUserContextAsync(long userId);
    Task<ResultStruct<UserContext?>> GetUserContextByExternalIdAsync(string externalId);
    Task<UserDbModel?> GetUserAsync(long userId);
    Task<bool> IsAdminAsync(long userId);
    Task<UserDbModel[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null);
    Task<UserDbModel[]> ListUsersAsync(long[] userIds, int? workspaceId);
    Task DeleteUserAsync(long userId);
    Task<UserDbModel> PutUserAsync(long userId, PutUserDto putUserDto);
    Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId);
    Task<(bool Success, string? ErrorCode)> MergeUsersAsync(long targetUserId, long sourceUserId);
}
