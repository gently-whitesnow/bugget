using Users.Entities.BO;
using Users.Entities.Dto.Users;

namespace Users.BO.Ports;

public interface IUsersRepository
{
    Task<User> TryInsertUserAsync(CreateUserDto createUserDto);
    Task<User?> GetUserAsync(long userId);
    Task<User?> GetUserByExternalIdAsync(string externalId);
    Task DeleteUserAsync(long userId);
    Task<User[]> AutocompleteUsersAsync(int workspaceId, string searchString, int skip, int take, int? teamId = null);
    Task<User[]> ListUsersAsync(long[] userIds, int? workspaceId);
    Task UpdateUserImageUrlAsync(long userId, string? imageUrl);
    Task<User> PutUserAsync(long userId, PutUserDto putUserDto);
    Task UpdateMattermostUserIdAsync(long userId, string? mattermostUserId);
    Task<bool> CheckUserOwnsWorkspacesAsync(long userId);
    Task MergeUsersAsync(long targetUserId, long sourceUserId);
}
