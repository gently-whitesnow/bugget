using Flow;
using Users.Entities.DbModels.Teams;

namespace Users.BO.Interfaces;

public interface ITeamsService
{
    Task<TeamDbModel[]> ListTeamsAsync(int[] workspaceIds);
    Task<TeamDbModel[]> ListTeamsAsync(int workspaceId, int[] teamIds);
    Task<TeamDbModel[]> AutocompleteTeamsAsync(int workspaceId, string searchString, int skip, int take);

    Task<ResultStruct<TeamDbModel>> CreateTeamAsync(int workspaceId, string name, long userId, int? userTeamId);

    Task<TeamDbModel> UpdateTeamAsync(int workspaceId, int teamId, string name);

    Task DeleteTeamAsync(int workspaceId, int teamId);
}
