using Flow;
using Users.Entities.DbModels.Teams;

namespace Users.DA.Interfaces;

public interface ITeamsRepository
{
    Task<TeamDbModel[]> ListTeamsAsync(int[] workspaceIds);
    Task<TeamDbModel[]> ListTeamsAsync(int workspaceId, int[] teamIds);
    Task<TeamDbModel[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take);
    Task<TeamDbModel> CreateTeamAsync(int workspaceId, string name);
    Task<ResultStruct<TeamDbModel>> CreateTeamAsync(int workspaceId, string name, int teamsCountLimit);
    Task<TeamDbModel> UpdateTeamAsync(int workspaceId, int teamId, string name);
    Task DeleteTeamAsync(int workspaceId, int teamId);
}
