using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Bugget.IntegrationTests.Users.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class UsersDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IUsersDbClient _usersDbClient;
    private readonly ITeamsDbClient _teamsDbClient;
    private readonly IWorkspacesDbClient _workspacesDbClient;
    private readonly ITeamMembersDbClient _teamMembersDbClient;
    private readonly IWorkspaceMembersDbClient _workspaceMembersDbClient;

    public UsersDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersDbClient>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsDbClient>();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesDbClient>();
        _teamMembersDbClient = scope.ServiceProvider.GetRequiredService<ITeamMembersDbClient>();
        _workspaceMembersDbClient = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersDbClient>();
    }

    [Fact(DisplayName = "Успешное создание пользователя")]
    public async Task InsertOrUpdateUserAsync_WhenNewUser_ShouldCreateUser()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Test User",
            ImageUrl = "https://example.com/avatar.jpg"
        };

        // Act
        var result = await _usersDbClient.TryInsertUserAsync(createUserDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(createUserDto.ExternalId, result.ExternalId);
        Assert.Equal(createUserDto.Name, result.Name);
        Assert.Equal(createUserDto.ImageUrl, result.ImageUrl);
        Assert.True(result.RegistrationDate > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "Успешное получение пользователя")]
    public async Task GetUserAsync_WhenUserExists_ShouldReturnUser()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Test User",
            ImageUrl = "https://example.com/avatar.jpg"
        };

        var createdUser = await _usersDbClient.TryInsertUserAsync(createUserDto);

        // Act
        var result = await _usersDbClient.GetUserAsync(createdUser.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createdUser.Id, result.Id);
        Assert.Equal(createdUser.ExternalId, result.ExternalId);
        Assert.Equal(createdUser.Name, result.Name);
        Assert.Equal(createdUser.ImageUrl, result.ImageUrl);
    }

    [Fact(DisplayName = "Получение пользователя, которого нет в базе возвращает null")]
    public async Task GetUserAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        const long nonExistentUserId = 999999;

        // Act
        var result = await _usersDbClient.GetUserAsync(nonExistentUserId);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Автодополнение пользователей в рабочем пространстве")]
    public async Task AutocompleteUsersAsync_WhenWorkspaceHasUsers_ShouldReturnMatchingUsers()
    {
        // Arrange
        var ownerCreateDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Test Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerCreateDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Act
        var result = await _usersDbClient.AutocompleteUsersAsync(team.WorkspaceId, "Test", 0, 10);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, u => u.Id == ownerUser.Id);
        Assert.All(result, user => Assert.Contains("Test", user.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Автодополнение пользователей, когда нет совпадений")]
    public async Task AutocompleteUsersAsync_WhenNoMatchingUsers_ShouldReturnEmptyArray()
    {
        // Arrange
        var ownerCreateDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Test Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerCreateDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Act - ищем пользователей с именем, которого нет
        var result = await _usersDbClient.AutocompleteUsersAsync(team.WorkspaceId, "NonExistentName", 0, 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Автодополнение пользователей с пагинацией")]
    public async Task AutocompleteUsersAsync_WithPagination_ShouldReturnCorrectPagedResults()
    {
        // Arrange - Создаем несколько пользователей с похожими именами
        var owner1Dto = new CreateUserDto
        {
            ExternalId = $"owner1_{Guid.NewGuid()}",
            Name = "Test Owner Alpha",
            ImageUrl = "https://example.com/owner1.jpg"
        };
        var owner1 = await _usersDbClient.TryInsertUserAsync(owner1Dto);
        var workspace1 = await _workspacesDbClient.CreateWorkspaceAsync(owner1.Id, "Workspace 1");
        var team1 = await _teamsDbClient.CreateTeamAsync(workspace1.Id, "Team 1");

        var owner2Dto = new CreateUserDto
        {
            ExternalId = $"owner2_{Guid.NewGuid()}",
            Name = "Test Owner Beta",
            ImageUrl = "https://example.com/owner2.jpg"
        };
        var owner2 = await _usersDbClient.TryInsertUserAsync(owner2Dto);
        var workspace2 = await _workspacesDbClient.CreateWorkspaceAsync(owner2.Id, "Workspace 2");
        var team2 = await _teamsDbClient.CreateTeamAsync(workspace2.Id, "Team 2");

        // Act - Запрашиваем первую страницу с лимитом 1
        var firstPageResult = await _usersDbClient.AutocompleteUsersAsync(team1.WorkspaceId, "Test", 0, 1);
        var allResults = await _usersDbClient.AutocompleteUsersAsync(team1.WorkspaceId, "Test", 0, 10);

        // Assert
        Assert.Single(firstPageResult);
        Assert.Single(allResults); // В организации только один владелец с "Test" в имени
        Assert.Equal(owner1.Id, firstPageResult[0].Id);
        Assert.Equal(owner1.Id, allResults[0].Id);
    }

    [Fact(DisplayName = "Автодополнение пользователей с пустым поисковым запросом")]
    public async Task AutocompleteUsersAsync_WithEmptySearchString_ShouldReturnAllUsersInWorkspace()
    {
        // Arrange
        var ownerCreateDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Organization Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerCreateDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Act
        var result = await _usersDbClient.AutocompleteUsersAsync(team.WorkspaceId, "", 0, 10);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, u => u.Id == ownerUser.Id);
    }

    [Fact(DisplayName = "Автодополнение пользователей с разными регистрами")]
    public async Task AutocompleteUsersAsync_CaseInsensitiveSearch_ShouldFindUsers()
    {
        // Arrange
        var ownerCreateDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Organization Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerCreateDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Act - поиск в разных регистрах
        var mixedCaseResult = await _usersDbClient.AutocompleteUsersAsync(team.WorkspaceId, "OrGaNiZaTiOn", 0, 10);

        // Assert
        Assert.NotEmpty(mixedCaseResult);
        Assert.Contains(mixedCaseResult, u => u.Id == ownerUser.Id);
    }

    [Fact(DisplayName = "Автодополнение пользователей в несуществующем рабочем пространстве")]
    public async Task AutocompleteUsersAsync_WithNonExistentWorkspace_ShouldReturnEmptyArray()
    {
        // Arrange
        const int nonExistentWorkspaceId = 999999;

        // Act
        var result = await _usersDbClient.AutocompleteUsersAsync(nonExistentWorkspaceId, "any", 0, 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Автодополнение пользователей с потенциально опасными символами")]
    public async Task AutocompleteUsersAsync_WithSpecialCharacters_ShouldBeSafeFromSqlInjection()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Test Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Act - тестируем различные потенциально опасные строки
        var searchStrings = new[]
        {
            "'; DROP TABLE users; --",
            "' OR '1'='1",
            "%'; SELECT * FROM users; --",
            "Test'; DELETE FROM users WHERE 1=1; --",
            "''; DROP DATABASE postgres; --"
        };

        foreach (var searchString in searchStrings)
        {
            // Assert - запросы должны выполняться безопасно без SQL injection
            var result = await _usersDbClient.AutocompleteUsersAsync(team.WorkspaceId, searchString, 0, 10);
            Assert.NotNull(result);

            // Проверяем что пользователь все еще существует (таблица не была удалена)
            var userCheck = await _usersDbClient.GetUserAsync(ownerUser.Id);
            Assert.NotNull(userCheck);
        }
    }

    #region AutocompleteUsersAsync Team Ranking Tests

    [Fact(DisplayName = "Автодополнение: члены команды ранжируются выше остальных")]
    public async Task AutocompleteUsersAsync_WithTeamId_ShouldRankTeamMembersFirst()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Alpha Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };
        var owner = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        // Создаём второго пользователя в том же workspace, но НЕ в команде
        var outsiderDto = new CreateUserDto
        {
            ExternalId = $"outsider_{Guid.NewGuid()}",
            Name = "Beta Outsider",
            ImageUrl = "https://example.com/outsider.jpg"
        };
        var outsider = await _usersDbClient.TryInsertUserAsync(outsiderDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(outsider.Id, workspace.Id, WorkspaceRole.Member);

        // Добавляем owner в команду
        await _teamMembersDbClient.CreateTeamMemberAsync(owner.Id, team.Id);

        // Act — поиск с teamId
        var result = await _usersDbClient.AutocompleteUsersAsync(workspace.Id, "", 0, 10, team.Id);

        // Assert — оба найдены, но член команды (Alpha Owner) первый
        Assert.True(result.Length >= 2);
        var ownerIndex = Array.FindIndex(result, u => u.Id == owner.Id);
        var outsiderIndex = Array.FindIndex(result, u => u.Id == outsider.Id);
        Assert.True(ownerIndex < outsiderIndex, "Член команды должен быть выше в результатах");
    }

    [Fact(DisplayName = "Автодополнение: без teamId сортировка по имени")]
    public async Task AutocompleteUsersAsync_WithoutTeamId_ShouldSortByNameOnly()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Zara Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };
        var owner = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        var memberDto = new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Anna Member",
            ImageUrl = "https://example.com/member.jpg"
        };
        var member = await _usersDbClient.TryInsertUserAsync(memberDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(member.Id, workspace.Id, WorkspaceRole.Member);

        // Act — без teamId
        var result = await _usersDbClient.AutocompleteUsersAsync(workspace.Id, "", 0, 10);

        // Assert — сортировка алфавитная: Anna < Zara
        Assert.True(result.Length >= 2);
        var annaIndex = Array.FindIndex(result, u => u.Id == member.Id);
        var zaraIndex = Array.FindIndex(result, u => u.Id == owner.Id);
        Assert.True(annaIndex < zaraIndex, "Без teamId сортировка должна быть по имени");
    }

    [Fact(DisplayName = "Автодополнение: внутри группы ранжирования сортировка по имени")]
    public async Task AutocompleteUsersAsync_WithTeamId_ShouldSortByNameWithinRankGroups()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Charlie Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };
        var owner = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");
        await _teamMembersDbClient.CreateTeamMemberAsync(owner.Id, team.Id);

        // Два члена команды
        var memberADto = new CreateUserDto
        {
            ExternalId = $"memberA_{Guid.NewGuid()}",
            Name = "Alice TeamMember",
            ImageUrl = "https://example.com/alice.jpg"
        };
        var memberA = await _usersDbClient.TryInsertUserAsync(memberADto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(memberA.Id, workspace.Id, WorkspaceRole.Member);
        await _teamMembersDbClient.CreateTeamMemberAsync(memberA.Id, team.Id);

        var memberBDto = new CreateUserDto
        {
            ExternalId = $"memberB_{Guid.NewGuid()}",
            Name = "Bob TeamMember",
            ImageUrl = "https://example.com/bob.jpg"
        };
        var memberB = await _usersDbClient.TryInsertUserAsync(memberBDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(memberB.Id, workspace.Id, WorkspaceRole.Member);
        await _teamMembersDbClient.CreateTeamMemberAsync(memberB.Id, team.Id);

        // Не-член команды с именем раньше по алфавиту
        var outsiderDto = new CreateUserDto
        {
            ExternalId = $"outsider_{Guid.NewGuid()}",
            Name = "Aaron Outsider",
            ImageUrl = "https://example.com/aaron.jpg"
        };
        var outsider = await _usersDbClient.TryInsertUserAsync(outsiderDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(outsider.Id, workspace.Id, WorkspaceRole.Member);

        // Act
        var result = await _usersDbClient.AutocompleteUsersAsync(workspace.Id, "", 0, 10, team.Id);

        // Assert — порядок: Alice, Bob, Charlie (члены команды по алфавиту), затем Aaron (не-член)
        var aliceIdx = Array.FindIndex(result, u => u.Id == memberA.Id);
        var bobIdx = Array.FindIndex(result, u => u.Id == memberB.Id);
        var charlieIdx = Array.FindIndex(result, u => u.Id == owner.Id);
        var aaronIdx = Array.FindIndex(result, u => u.Id == outsider.Id);

        Assert.True(aliceIdx >= 0 && bobIdx >= 0 && charlieIdx >= 0 && aaronIdx >= 0,
            "Все пользователи должны быть в результатах");
        Assert.True(aliceIdx < bobIdx, "Alice < Bob внутри группы команды");
        Assert.True(bobIdx < charlieIdx, "Bob < Charlie внутри группы команды");
        Assert.True(charlieIdx < aaronIdx, "Члены команды (Charlie) выше не-членов (Aaron)");
    }

    [Fact(DisplayName = "Автодополнение: ранжирование работает с фильтром по имени")]
    public async Task AutocompleteUsersAsync_WithTeamIdAndSearchString_ShouldRankAndFilter()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "RankTest Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };
        var owner = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        var memberDto = new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "RankTest Member",
            ImageUrl = "https://example.com/member.jpg"
        };
        var member = await _usersDbClient.TryInsertUserAsync(memberDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(member.Id, workspace.Id, WorkspaceRole.Member);
        await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id);

        var unrelatedDto = new CreateUserDto
        {
            ExternalId = $"unrelated_{Guid.NewGuid()}",
            Name = "Completely Different",
            ImageUrl = "https://example.com/unrelated.jpg"
        };
        var unrelated = await _usersDbClient.TryInsertUserAsync(unrelatedDto);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(unrelated.Id, workspace.Id, WorkspaceRole.Member);

        // Act — поиск "RankTest" с teamId
        var result = await _usersDbClient.AutocompleteUsersAsync(workspace.Id, "RankTest", 0, 10, team.Id);

        // Assert — только два с "RankTest", член команды первый
        Assert.Equal(2, result.Length);
        Assert.Equal(member.Id, result[0].Id);
        Assert.Equal(owner.Id, result[1].Id);
    }

    #endregion

    [Fact(DisplayName = "Получение списка пользователей по ID")]
    public async Task ListUsersAsync_WhenUsersExist_ShouldReturnRequestedUsers()
    {
        // Arrange
        var user1Dto = new CreateUserDto
        {
            ExternalId = $"user1_{Guid.NewGuid()}",
            Name = "User One",
            ImageUrl = "https://example.com/user1.jpg"
        };
        var user1 = await _usersDbClient.TryInsertUserAsync(user1Dto);
        var workspace1 = await _workspacesDbClient.CreateWorkspaceAsync(user1.Id, "Workspace 1");

        var user2Dto = new CreateUserDto
        {
            ExternalId = $"user2_{Guid.NewGuid()}",
            Name = "User Two",
            ImageUrl = "https://example.com/user2.jpg"
        };
        var user2 = await _usersDbClient.TryInsertUserAsync(user2Dto);
        var workspace2 = await _workspacesDbClient.CreateWorkspaceAsync(user2.Id, "Workspace 2");

        var userIds = new List<long> { user1.Id, user2.Id };

        // Act
        var result = await _usersDbClient.ListUsersAsync(userIds.ToArray(), workspace1.Id);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, u => u.Id == user1.Id);
        Assert.DoesNotContain(result, u => u.Id == user2.Id);
        Assert.Single(result);

        var result2 = await _usersDbClient.ListUsersAsync(userIds.ToArray(), workspace2.Id);
        Assert.NotEmpty(result2);
        Assert.Contains(result2, u => u.Id == user2.Id);
        Assert.DoesNotContain(result2, u => u.Id == user1.Id);
        Assert.Single(result2);
    }

    [Fact(DisplayName = "Получение списка пользователей с несуществующими ID")]
    public async Task ListUsersAsync_WithNonExistentUserIds_ShouldReturnEmptyArray()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Test Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        var nonExistentUserIds = new List<long> { 999999, 999998 };

        // Act
        var result = await _usersDbClient.ListUsersAsync(nonExistentUserIds.ToArray(), team.WorkspaceId);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Получение списка пользователей с пустым списком ID")]
    public async Task ListUsersAsync_WithEmptyUserIdsList_ShouldReturnEmptyArray()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Test Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersDbClient.TryInsertUserAsync(ownerDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(ownerUser.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        var emptyUserIds = new List<long>();

        // Act
        var result = await _usersDbClient.ListUsersAsync(emptyUserIds.ToArray(), team.WorkspaceId);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Получение списка пользователей в несуществующем рабочем пространстве")]
    public async Task ListUsersAsync_WithNonExistentWorkspace_ShouldReturnEmptyArray()
    {
        // Arrange
        var userDto = new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "Test User",
            ImageUrl = "https://example.com/user.jpg"
        };

        var user = await _usersDbClient.TryInsertUserAsync(userDto);
        const int nonExistentWorkspaceId = 999999;
        var userIds = new List<long> { user.Id };

        // Act
        var result = await _usersDbClient.ListUsersAsync(userIds.ToArray(), nonExistentWorkspaceId);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Получение списка пользователей с частично существующими ID")]
    public async Task ListUsersAsync_WithMixedExistingAndNonExistentIds_ShouldReturnOnlyExistingUsers()
    {
        // Arrange
        var user1Dto = new CreateUserDto
        {
            ExternalId = $"user1_{Guid.NewGuid()}",
            Name = "Existing User",
            ImageUrl = "https://example.com/user1.jpg"
        };
        var user1 = await _usersDbClient.TryInsertUserAsync(user1Dto);
        var workspace1 = await _workspacesDbClient.CreateWorkspaceAsync(user1.Id, "Workspace 1");
        var team1 = await _teamsDbClient.CreateTeamAsync(workspace1.Id, "Team 1");

        var mixedUserIds = new List<long> { user1.Id, 999999, 999998 };

        // Act
        var result = await _usersDbClient.ListUsersAsync(mixedUserIds.ToArray(), team1.WorkspaceId);

        // Assert
        Assert.Single(result);
        Assert.Equal(user1.Id, result[0].Id);
        Assert.Equal("Existing User", result[0].Name);
    }

    [Fact(DisplayName = "Получение списка пользователей с дублирующимися ID")]
    public async Task ListUsersAsync_WithDuplicateUserIds_ShouldReturnUniqueUsers()
    {
        // Arrange
        var userDto = new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "Test User",
            ImageUrl = "https://example.com/user.jpg"
        };

        var user = await _usersDbClient.TryInsertUserAsync(userDto);
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(user.Id, "Test Workspace");
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Test Team");

        var duplicateUserIds = new List<long> { user.Id, user.Id, user.Id };

        // Act
        var result = await _usersDbClient.ListUsersAsync(duplicateUserIds.ToArray(), workspace.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(user.Id, result[0].Id);
    }

    [Fact(DisplayName = "Успешное удаление пользователя")]
    public async Task DeleteUserAsync_WhenUserExists_ShouldDeleteUser()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Test User To Delete",
            ImageUrl = "https://example.com/avatar.jpg"
        };

        var createdUser = await _usersDbClient.TryInsertUserAsync(createUserDto);

        // Убеждаемся, что пользователь создан
        var userBeforeDeletion = await _usersDbClient.GetUserAsync(createdUser.Id);
        Assert.NotNull(userBeforeDeletion);

        // Act
        await _usersDbClient.DeleteUserAsync(createdUser.Id);

        // Assert
        var userAfterDeletion = await _usersDbClient.GetUserAsync(createdUser.Id);
        Assert.Null(userAfterDeletion);
    }

    [Fact(DisplayName = "Удаление несуществующего пользователя не вызывает исключение")]
    public async Task DeleteUserAsync_WhenUserDoesNotExist_ShouldNotThrowException()
    {
        // Arrange
        const long nonExistentUserId = 999999;

        // Act & Assert - не должно вызывать исключение
        var exception = await Record.ExceptionAsync(() => _usersDbClient.DeleteUserAsync(nonExistentUserId));
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Повторное удаление уже удаленного пользователя не вызывает исключение")]
    public async Task DeleteUserAsync_WhenUserAlreadyDeleted_ShouldNotThrowException()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Test User To Delete Twice",
            ImageUrl = "https://example.com/avatar.jpg"
        };

        var createdUser = await _usersDbClient.TryInsertUserAsync(createUserDto);

        // Первое удаление
        await _usersDbClient.DeleteUserAsync(createdUser.Id);

        // Убеждаемся, что пользователь удален
        var userAfterFirstDeletion = await _usersDbClient.GetUserAsync(createdUser.Id);
        Assert.Null(userAfterFirstDeletion);

        // Act & Assert - повторное удаление не должно вызывать исключение
        var exception = await Record.ExceptionAsync(() => _usersDbClient.DeleteUserAsync(createdUser.Id));
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Удаление пользователя не влияет на других пользователей")]
    public async Task DeleteUserAsync_ShouldNotAffectOtherUsers()
    {
        // Arrange
        var user1Dto = new CreateUserDto
        {
            ExternalId = $"user1_{Guid.NewGuid()}",
            Name = "User One",
            ImageUrl = "https://example.com/user1.jpg"
        };

        var user2Dto = new CreateUserDto
        {
            ExternalId = $"user2_{Guid.NewGuid()}",
            Name = "User Two",
            ImageUrl = "https://example.com/user2.jpg"
        };

        var user1 = await _usersDbClient.TryInsertUserAsync(user1Dto);
        var user2 = await _usersDbClient.TryInsertUserAsync(user2Dto);

        // Act
        await _usersDbClient.DeleteUserAsync(user1.Id);

        // Assert
        var deletedUser = await _usersDbClient.GetUserAsync(user1.Id);
        var remainingUser = await _usersDbClient.GetUserAsync(user2.Id);

        Assert.Null(deletedUser);
        Assert.NotNull(remainingUser);
        Assert.Equal(user2.Id, remainingUser.Id);
        Assert.Equal("User Two", remainingUser.Name);
    }

    #region UpdateUserImageUrlAsync Tests

    [Fact(DisplayName = "Успешное обновление URL аватара пользователя")]
    public async Task UpdateUserImageUrlAsync_WhenUserExists_ShouldUpdateImageUrl()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Test User",
            ImageUrl = "https://example.com/old-avatar.jpg"
        };

        var createdUser = await _usersDbClient.TryInsertUserAsync(createUserDto);
        var newImageUrl = "https://example.com/new-avatar.jpg";

        // Act
        await _usersDbClient.UpdateUserImageUrlAsync(createdUser.Id, newImageUrl);

        // Assert
        var updatedUser = await _usersDbClient.GetUserAsync(createdUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(newImageUrl, updatedUser.ImageUrl);
    }

    [Fact(DisplayName = "Обновление URL аватара несуществующего пользователя не вызывает исключение")]
    public async Task UpdateUserImageUrlAsync_WhenUserDoesNotExist_ShouldNotThrowException()
    {
        // Arrange
        const long nonExistentUserId = 999999;
        var newImageUrl = "https://example.com/avatar.jpg";

        // Act & Assert - не должно вызывать исключение
        var exception = await Record.ExceptionAsync(() =>
            _usersDbClient.UpdateUserImageUrlAsync(nonExistentUserId, newImageUrl));
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Обновление URL аватара не влияет на других пользователей")]
    public async Task UpdateUserImageUrlAsync_ShouldNotAffectOtherUsers()
    {
        // Arrange
        var user1Dto = new CreateUserDto
        {
            ExternalId = $"user1_{Guid.NewGuid()}",
            Name = "User One",
            ImageUrl = "https://example.com/user1.jpg"
        };

        var user2Dto = new CreateUserDto
        {
            ExternalId = $"user2_{Guid.NewGuid()}",
            Name = "User Two",
            ImageUrl = "https://example.com/user2.jpg"
        };

        var user1 = await _usersDbClient.TryInsertUserAsync(user1Dto);
        var user2 = await _usersDbClient.TryInsertUserAsync(user2Dto);

        var newImageUrl = "https://example.com/user1-new.jpg";

        // Act
        await _usersDbClient.UpdateUserImageUrlAsync(user1.Id, newImageUrl);

        // Assert
        var updatedUser1 = await _usersDbClient.GetUserAsync(user1.Id);
        var unchangedUser2 = await _usersDbClient.GetUserAsync(user2.Id);

        Assert.NotNull(updatedUser1);
        Assert.NotNull(unchangedUser2);
        Assert.Equal(newImageUrl, updatedUser1.ImageUrl);
        Assert.Equal("https://example.com/user2.jpg", unchangedUser2.ImageUrl);
    }

    #endregion

    #region PutUserAsync Tests

    [Fact(DisplayName = "Успешное обновление данных пользователя")]
    public async Task PutUserAsync_WhenUserExists_ShouldUpdateAndReturnUser()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"test_user_{Guid.NewGuid()}",
            Name = "Original Name",
            ImageUrl = "https://example.com/avatar.jpg"
        };

        var createdUser = await _usersDbClient.TryInsertUserAsync(createUserDto);
        var putUserDto = new PutUserDto { Name = "Updated Name" };

        // Act
        var result = await _usersDbClient.PutUserAsync(createdUser.Id, putUserDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createdUser.Id, result.Id);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(createdUser.ExternalId, result.ExternalId);
    }


    [Fact(DisplayName = "Обновление данных несуществующего пользователя вызывает исключение")]
    public async Task PutUserAsync_WhenUserDoesNotExist_ShouldThrowException()
    {
        // Arrange
        const long nonExistentUserId = 999999;
        var putUserDto = new PutUserDto { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _usersDbClient.PutUserAsync(nonExistentUserId, putUserDto));
    }

    [Fact(DisplayName = "Обновление данных пользователя не влияет на других пользователей")]
    public async Task PutUserAsync_ShouldNotAffectOtherUsers()
    {
        // Arrange
        var user1Dto = new CreateUserDto
        {
            ExternalId = $"user1_{Guid.NewGuid()}",
            Name = "User One Original",
            ImageUrl = "https://example.com/user1.jpg"
        };

        var user2Dto = new CreateUserDto
        {
            ExternalId = $"user2_{Guid.NewGuid()}",
            Name = "User Two Original",
            ImageUrl = "https://example.com/user2.jpg"
        };

        var user1 = await _usersDbClient.TryInsertUserAsync(user1Dto);
        var user2 = await _usersDbClient.TryInsertUserAsync(user2Dto);

        var putUserDto = new PutUserDto { Name = "User One Updated" };

        // Act
        await _usersDbClient.PutUserAsync(user1.Id, putUserDto);

        // Assert
        var updatedUser1 = await _usersDbClient.GetUserAsync(user1.Id);
        var unchangedUser2 = await _usersDbClient.GetUserAsync(user2.Id);

        Assert.NotNull(updatedUser1);
        Assert.NotNull(unchangedUser2);
        Assert.Equal("User One Updated", updatedUser1.Name);
        Assert.Equal("User Two Original", unchangedUser2.Name);
    }

    #endregion
}
