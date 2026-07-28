using System;
using System.Linq;
using Users.Api.Contracts.Generated;
using Users.Api.Controllers.TeamMembers;
using Users.Api.Controllers.Teams;
using Users.Api.Controllers.Users;
using Users.Api.Controllers.Workspaces;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Teams;
using Users.Entities.DbModels.Users;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.View.Users;

namespace Users.Api.Mappers;

/// <summary>
/// View/DbModel → Contracts для фронтовой поверхности модуля users. Контрактные
/// DTO сгенерированы из <c>specs/contracts/users/openapi.yaml</c>.
///
/// Формы намеренно повторяют то, что уходило фронту до перехода на contract-first:
/// контракт описан с работающего API, а не наоборот. Доказательство — снимки в
/// <c>Bugget.IntegrationTests/Contract/Snapshots</c>.
/// </summary>
internal static class UsersContractMapper
{
    public static User ToContract(this UserView view) => new()
    {
        Id = view.Id,
        Name = view.Name,
        Image_url = view.ImageUrl,
        Workspace_role = view.WorkspaceRole,
        Mattermost_user_id = view.MattermostUserId,
    };

    public static UserProfile ToContract(this UserDbModel model) => new()
    {
        Id = model.Id,
        External_id = model.ExternalId,
        Name = model.Name,
        Image_url = model.ImageUrl,
        Mattermost_user_id = model.MattermostUserId,
        Registration_date = model.RegistrationDate,
        Updated_at = model.UpdatedAt,
    };

    public static AutocompleteUsers ToContract(this AutocompleteUsersView view) => new()
    {
        Users = [.. view.Users.Select(user => new AutocompleteUser
        {
            Id = user.Id,
            Name = user.Name,
            Image_url = user.ImageUrl,
        })],
        Total = view.Total,
    };

    public static ExternalLink ToContract(this ExternalLinkView view) => new()
    {
        Provider = view.Provider,
        External_id = view.ExternalId,
        Email = view.Email,
        Linked_at = view.LinkedAt,
    };

    public static Workspace ToContract(this WorkspaceDbModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static WorkspaceMember ToContract(this WorkspaceMemberDbModel model) => new()
    {
        Workspace_id = model.WorkspaceId,
        User_id = model.UserId,
        Role = model.Role,
        Created_at = model.CreatedAt,
    };

    public static Team ToContract(this TeamDbModel model) => new()
    {
        Id = model.Id,
        Workspace_id = model.WorkspaceId,
        Name = model.Name,
        Created_at = model.CreatedAt,
        Updated_at = model.UpdatedAt,
    };

    public static TeamSummary ToContract(this TeamView view) => new()
    {
        Id = view.Id,
        Name = view.Name,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
    };

    public static AutocompleteTeams ToContract(this AutocompleteTeamsView view) => new()
    {
        Teams = [.. view.Teams.Select(ToContract)],
        Total = view.Total,
    };

    public static TeamMemberSummary ToContract(this TeamMemberView view) => new()
    {
        Team_id = view.TeamId,
        User_id = view.UserId,
        Created_at = view.CreatedAt,
    };

    public static WorkspaceMemberSummary ToContract(this WorkspaceMemberView view) => new()
    {
        Workspace_id = view.WorkspaceId,
        User_id = view.UserId,
        Role = view.Role,
        Created_at = view.CreatedAt,
    };

    public static TeamMembers ToContract(this TeamMembersView view) => new()
    {
        Members = [.. view.Members.Select(ToContract)],
        Size_limit = view.SizeLimit,
    };

    public static WorkspaceWithTeams ToContract(this WorkspaceView view) => new()
    {
        Id = view.Id,
        Name = view.Name,
        Created_at = view.CreatedAt,
        Updated_at = view.UpdatedAt,
        // null здесь — часть контракта: «не запрашивали», в отличие от пустого списка.
        Teams = view.Teams is null ? null : [.. view.Teams.Select(ToContract)],
    };

    public static WorkspacesContext ToContract(this WorkspacesContextView view) => new()
    {
        Workspaces = [.. view.Workspaces.Select(ToContract)],
        Teams_member = view.TeamsMember is null ? null : [.. view.TeamsMember.Select(ToContract)],
        Workspaces_member = view.WorkspacesMember is null ? null : [.. view.WorkspacesMember.Select(ToContract)],
    };

}
