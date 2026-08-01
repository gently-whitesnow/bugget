using Bugget.Domain.Errors;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface ITeamsRepository
{
    Task<Team[]> ListTeamsAsync(int[] workspaceIds);
    Task<Team[]> ListTeamsAsync(int workspaceId, int[] teamIds);
    Task<Team[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take);
    Task<Team> CreateTeamAsync(int workspaceId, string name);
    Task<(Team? Value, Error? Error)> CreateTeamAsync(int workspaceId, string name, int teamsCountLimit);
    Task<Team> UpdateTeamAsync(int workspaceId, int teamId, string name);
    Task DeleteTeamAsync(int workspaceId, int teamId);
}
