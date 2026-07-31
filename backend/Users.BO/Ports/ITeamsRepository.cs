using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.Ports;

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
