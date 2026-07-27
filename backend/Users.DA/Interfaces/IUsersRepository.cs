using Users.Entities.DbModels.Events;
using Users.Entities.DbModels.Users;
using Users.Entities.Dto.Users;

namespace Users.DA.Interfaces;

public interface IUsersRepository
{
    Task<UserDbModel> TryInsertUserAsync(CreateUserDto createUserDto);
    Task<UserDbModel?> GetUserAsync(long userId);
    Task<UserDbModel?> GetUserByExternalIdAsync(string externalId);
    Task DeleteUserAsync(long userId);
    Task<UserDbModel[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null);
    Task<UserDbModel[]> ListUsersAsync(long[] userIds, int? workspaceId);
    Task UpdateUserImageUrlAsync(long userId, string? imageUrl);
    Task<UserDbModel> PutUserAsync(long userId, PutUserDto putUserDto);
    Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId);
    Task<bool> CheckUserOwnsWorkspacesAsync(long userId);
    Task MergeUsersAsync(long targetUserId, long sourceUserId);
}
