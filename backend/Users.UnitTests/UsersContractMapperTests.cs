using System;
using System.Linq;
using Users.Api.Controllers.TeamInvites;
using Users.Api.Controllers.TeamMembers;
using Users.Api.Controllers.Teams;
using Users.Api.Controllers.Users;
using Users.Api.Controllers.Workspaces;
using Users.Api.Mappers;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Teams;
using Users.Entities.DbModels.Users;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.View.Users;
using Xunit;

namespace Users.UnitTests;

/// <summary>
/// Маппер во внешний контракт: проверяется то, что снимки контракта поймать не могут —
/// различие «поле пустое» и «поле не запрашивали». Снимок видит форму одного ответа,
/// а здесь фиксируется, что `null` не превращается в пустой список и наоборот.
/// </summary>
public class UsersContractMapperTests
{
    private static readonly DateTimeOffset Moment = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Контекст рабочих пространств: null-коллекции остаются null")]
    public void WorkspacesContext_keeps_nulls()
    {
        var view = new WorkspacesContextView
        {
            Workspaces = [Workspace(teams: null)],
            TeamsMember = null,
            WorkspacesMember = null,
        };

        var contract = view.ToContract();

        Assert.Null(contract.Teams_member);
        Assert.Null(contract.Workspaces_member);
        Assert.Null(Assert.Single(contract.Workspaces).Teams);
    }

    [Fact(DisplayName = "Контекст рабочих пространств: заполненные коллекции переносятся целиком")]
    public void WorkspacesContext_maps_collections()
    {
        var view = new WorkspacesContextView
        {
            Workspaces = [Workspace(teams: [Team()])],
            TeamsMember = [new TeamMemberView { TeamId = "7", UserId = "42", CreatedAt = Moment }],
            WorkspacesMember =
            [
                new WorkspaceMemberView { WorkspaceId = "1", UserId = "42", Role = "admin", CreatedAt = Moment }
            ],
        };

        var contract = view.ToContract();

        var workspace = Assert.Single(contract.Workspaces);
        Assert.Equal("1", workspace.Id);
        Assert.Equal("рабочее пространство", workspace.Name);
        Assert.Equal("7", Assert.Single(workspace.Teams!).Id);
        Assert.Equal("42", Assert.Single(contract.Teams_member!).User_id);
        Assert.Equal("admin", Assert.Single(contract.Workspaces_member!).Role);
    }

    [Fact(DisplayName = "Пользователь: пустые поля профиля остаются null")]
    public void User_keeps_optional_fields_null()
    {
        var contract = new UserView("42", "Тестер", null, "member", null).ToContract();

        Assert.Equal("42", contract.Id);
        Assert.Equal("Тестер", contract.Name);
        Assert.Equal("member", contract.Workspace_role);
        Assert.Null(contract.Image_url);
        Assert.Null(contract.Mattermost_user_id);
    }

    [Fact(DisplayName = "Профиль: все поля модели попадают в контракт")]
    public void UserProfile_maps_all_fields()
    {
        var contract = new UserDbModel
        {
            Id = 42,
            ExternalId = "keycloak|42",
            Name = "Тестер",
            ImageUrl = "avatars/42.webp",
            MattermostUserId = "mm-42",
            RegistrationDate = Moment,
            UpdatedAt = Moment,
        }.ToContract();

        Assert.Equal(42, contract.Id);
        Assert.Equal("keycloak|42", contract.External_id);
        Assert.Equal("avatars/42.webp", contract.Image_url);
        Assert.Equal("mm-42", contract.Mattermost_user_id);
        Assert.Equal(Moment, contract.Registration_date);
        Assert.Equal(Moment, contract.Updated_at);
    }

    [Fact(DisplayName = "Подсказки пользователей: элементы и общее количество переносятся")]
    public void AutocompleteUsers_maps_items()
    {
        var contract = new AutocompleteUsersView
        {
            Users = [new AutocompleteUserView { Id = "42", Name = "Тестер", ImageUrl = null }],
            Total = 1,
        }.ToContract();

        Assert.Equal(1, contract.Total);
        var user = Assert.Single(contract.Users);
        Assert.Equal("42", user.Id);
        Assert.Null(user.Image_url);
    }

    [Fact(DisplayName = "Подсказки команд: элементы и общее количество переносятся")]
    public void AutocompleteTeams_maps_items()
    {
        var contract = new AutocompleteTeamsView { Teams = [Team()], Total = 1 }.ToContract();

        Assert.Equal(1, contract.Total);
        Assert.Equal("команда", Assert.Single(contract.Teams).Name);
    }

    [Fact(DisplayName = "Участники команды: состав и лимит размера переносятся")]
    public void TeamMembers_maps_members_and_limit()
    {
        var contract = new TeamMembersView
        {
            Members = [new TeamMemberView { TeamId = "7", UserId = "42", CreatedAt = Moment }],
            SizeLimit = 10,
        }.ToContract();

        Assert.Equal(10, contract.Size_limit);
        Assert.Equal("7", Assert.Single(contract.Members).Team_id);
    }

    [Fact(DisplayName = "Рабочее пространство и команда: числовые идентификаторы сохраняются числами")]
    public void Workspace_and_team_keep_numeric_ids()
    {
        var workspace = new WorkspaceDbModel { Id = 1, Name = "пространство", CreatedAt = Moment, UpdatedAt = Moment }
            .ToContract();
        var team = new TeamDbModel { Id = 7, WorkspaceId = 1, Name = "команда", CreatedAt = Moment, UpdatedAt = Moment }
            .ToContract();

        Assert.Equal(1, workspace.Id);
        Assert.Equal(7, team.Id);
        Assert.Equal(1, team.Workspace_id);
    }

    [Fact(DisplayName = "Членство в рабочем пространстве: идентификаторы и роль переносятся")]
    public void WorkspaceMember_maps_fields()
    {
        var contract = new WorkspaceMemberDbModel
        {
            WorkspaceId = 1,
            UserId = 42,
            Role = "admin",
            CreatedAt = Moment,
        }.ToContract();

        Assert.Equal(1, contract.Workspace_id);
        Assert.Equal(42, contract.User_id);
        Assert.Equal("admin", contract.Role);
    }

    [Fact(DisplayName = "Приглашения: ссылка отдаётся только там, где она есть")]
    public void Invites_map_link_only_where_present()
    {
        var withLink = new TeamCreateInviteView
        {
            Id = 3,
            InviteLink = "https://bugget/invite/token",
            CreatedAt = Moment,
            ExpiresAt = Moment,
        }.ToContract();
        var withoutLink = new TeamInviteView { Id = 3, CreatedAt = Moment, ExpiresAt = Moment }.ToContract();
        var accepted = new AcceptInviteView(3, 7, 1).ToContract();

        Assert.Equal("https://bugget/invite/token", withLink.Invite_link);
        Assert.Equal(3, withoutLink.Id);
        Assert.Equal(7, accepted.Team_id);
        Assert.Equal(1, accepted.Workspace_id);
    }

    [Fact(DisplayName = "Привязка провайдера: почты может не быть")]
    public void ExternalLink_allows_missing_email()
    {
        var contract = new ExternalLinkView("mattermost", "mm-42", null, Moment).ToContract();

        Assert.Equal("mattermost", contract.Provider);
        Assert.Equal("mm-42", contract.External_id);
        Assert.Null(contract.Email);
        Assert.Equal(Moment, contract.Linked_at);
    }

    private static WorkspaceView Workspace(TeamView[]? teams) => new()
    {
        Id = "1",
        Name = "рабочее пространство",
        CreatedAt = Moment,
        UpdatedAt = Moment,
        Teams = teams,
    };

    private static TeamView Team() => new()
    {
        Id = "7",
        Name = "команда",
        CreatedAt = Moment,
        UpdatedAt = Moment,
    };
}
