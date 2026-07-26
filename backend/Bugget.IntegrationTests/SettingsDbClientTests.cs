using System.Linq;
using Bugget.DA.Interfaces;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class SettingsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly ISettingsDbClient _settingsDbClient;

    public SettingsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _settingsDbClient = scope.ServiceProvider.GetRequiredService<ISettingsDbClient>();
    }

    [Fact(DisplayName = "UpsertWorkspaceSettingAsync создаёт и обновляет значение")]
    public async Task UpsertWorkspaceSettingAsync_ShouldCreateAndUpdate()
    {
        var workspaceId = $"ws_{Guid.NewGuid()}";
        var sectionId = "section_a";
        var settingId = "key_a";

        var first = await _settingsDbClient.UpsertWorkspaceSettingAsync(workspaceId, sectionId, settingId, "v1");
        var second = await _settingsDbClient.UpsertWorkspaceSettingAsync(workspaceId, sectionId, settingId, "v2");
        var (workspaceSettings, _, _) = await _settingsDbClient.GetSettingsAsync(workspaceId, string.Empty, string.Empty);

        Assert.Equal("v1", first.FieldValue);
        Assert.Equal("v2", second.FieldValue);

        var stored = workspaceSettings.Single(s => s.FeatureKey == sectionId && s.FieldKey == settingId);
        Assert.Equal("v2", stored.FieldValue);
    }

    [Fact(DisplayName = "UpsertWorkspaceSettingsAsync заменяет все значения ключа")]
    public async Task UpsertWorkspaceSettingsAsync_ShouldReplaceValues()
    {
        var workspaceId = $"ws_{Guid.NewGuid()}";
        var sectionId = "section_b";
        var settingId = "key_b";

        await _settingsDbClient.UpsertWorkspaceSettingsAsync(workspaceId, sectionId, settingId, ["v1", "v2"]);
        await _settingsDbClient.UpsertWorkspaceSettingsAsync(workspaceId, sectionId, settingId, ["v3"]);

        var (workspaceSettings, _, _) = await _settingsDbClient.GetSettingsAsync(workspaceId, string.Empty, string.Empty);
        var values = workspaceSettings
            .Where(s => s.FeatureKey == sectionId && s.FieldKey == settingId)
            .Select(s => s.FieldValue)
            .ToArray();

        Assert.Single(values);
        Assert.Equal("v3", values[0]);
    }

    [Fact(DisplayName = "UpsertWorkspaceSettingsAsync с пустым массивом удаляет ключ")]
    public async Task UpsertWorkspaceSettingsAsync_WithEmpty_ShouldDeleteKey()
    {
        var workspaceId = $"ws_{Guid.NewGuid()}";
        var sectionId = "section_c";
        var settingId = "key_c";

        await _settingsDbClient.UpsertWorkspaceSettingsAsync(workspaceId, sectionId, settingId, ["v1"]);
        await _settingsDbClient.UpsertWorkspaceSettingsAsync(workspaceId, sectionId, settingId, Array.Empty<string>());

        var (workspaceSettings, _, _) = await _settingsDbClient.GetSettingsAsync(workspaceId, string.Empty, string.Empty);
        Assert.DoesNotContain(workspaceSettings, s => s.FeatureKey == sectionId && s.FieldKey == settingId);
    }

    [Fact(DisplayName = "UpsertTeamSettingAsync создаёт и обновляет значение")]
    public async Task UpsertTeamSettingAsync_ShouldCreateAndUpdate()
    {
        var teamId = $"team_{Guid.NewGuid()}";
        var sectionId = "section_t1";
        var settingId = "key_t1";

        var first = await _settingsDbClient.UpsertTeamSettingAsync(teamId, sectionId, settingId, "v1");
        var second = await _settingsDbClient.UpsertTeamSettingAsync(teamId, sectionId, settingId, "v2");
        var (_, teamSettings, _) = await _settingsDbClient.GetSettingsAsync(string.Empty, teamId, string.Empty);

        Assert.Equal("v1", first.FieldValue);
        Assert.Equal("v2", second.FieldValue);

        var stored = teamSettings.Single(s => s.FeatureKey == sectionId && s.FieldKey == settingId);
        Assert.Equal("v2", stored.FieldValue);
    }

    [Fact(DisplayName = "UpsertTeamSettingsAsync заменяет все значения ключа")]
    public async Task UpsertTeamSettingsAsync_ShouldReplaceValues()
    {
        var teamId = $"team_{Guid.NewGuid()}";
        var sectionId = "section_t2";
        var settingId = "key_t2";

        await _settingsDbClient.UpsertTeamSettingsAsync(teamId, sectionId, settingId, ["v1", "v2"]);
        await _settingsDbClient.UpsertTeamSettingsAsync(teamId, sectionId, settingId, ["v3"]);

        var (_, teamSettings, _) = await _settingsDbClient.GetSettingsAsync(string.Empty, teamId, string.Empty);
        var values = teamSettings
            .Where(s => s.FeatureKey == sectionId && s.FieldKey == settingId)
            .Select(s => s.FieldValue)
            .ToArray();

        Assert.Single(values);
        Assert.Equal("v3", values[0]);
    }

    [Fact(DisplayName = "UpsertTeamSettingsAsync с пустым массивом удаляет ключ")]
    public async Task UpsertTeamSettingsAsync_WithEmpty_ShouldDeleteKey()
    {
        var teamId = $"team_{Guid.NewGuid()}";
        var sectionId = "section_t3";
        var settingId = "key_t3";

        await _settingsDbClient.UpsertTeamSettingsAsync(teamId, sectionId, settingId, ["v1"]);
        await _settingsDbClient.UpsertTeamSettingsAsync(teamId, sectionId, settingId, Array.Empty<string>());

        var (_, teamSettings, _) = await _settingsDbClient.GetSettingsAsync(string.Empty, teamId, string.Empty);
        Assert.DoesNotContain(teamSettings, s => s.FeatureKey == sectionId && s.FieldKey == settingId);
    }

    [Fact(DisplayName = "UpsertUserSettingAsync создаёт и обновляет значение")]
    public async Task UpsertUserSettingAsync_ShouldCreateAndUpdate()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var sectionId = "section_u1";
        var settingId = "key_u1";

        var first = await _settingsDbClient.UpsertUserSettingAsync(userId, sectionId, settingId, "v1");
        var second = await _settingsDbClient.UpsertUserSettingAsync(userId, sectionId, settingId, "v2");
        var (_, _, userSettings) = await _settingsDbClient.GetSettingsAsync(string.Empty, string.Empty, userId);

        Assert.Equal("v1", first.FieldValue);
        Assert.Equal("v2", second.FieldValue);

        var stored = userSettings.Single(s => s.FeatureKey == sectionId && s.FieldKey == settingId);
        Assert.Equal("v2", stored.FieldValue);
    }

    [Fact(DisplayName = "UpsertUserSettingsAsync заменяет все значения ключа")]
    public async Task UpsertUserSettingsAsync_ShouldReplaceValues()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var sectionId = "section_u2";
        var settingId = "key_u2";

        await _settingsDbClient.UpsertUserSettingsAsync(userId, sectionId, settingId, ["v1", "v2"]);
        await _settingsDbClient.UpsertUserSettingsAsync(userId, sectionId, settingId, ["v3"]);

        var (_, _, userSettings) = await _settingsDbClient.GetSettingsAsync(string.Empty, string.Empty, userId);
        var values = userSettings
            .Where(s => s.FeatureKey == sectionId && s.FieldKey == settingId)
            .Select(s => s.FieldValue)
            .ToArray();

        Assert.Single(values);
        Assert.Equal("v3", values[0]);
    }

    [Fact(DisplayName = "UpsertUserSettingsAsync с пустым массивом удаляет ключ")]
    public async Task UpsertUserSettingsAsync_WithEmpty_ShouldDeleteKey()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var sectionId = "section_u3";
        var settingId = "key_u3";

        await _settingsDbClient.UpsertUserSettingsAsync(userId, sectionId, settingId, ["v1"]);
        await _settingsDbClient.UpsertUserSettingsAsync(userId, sectionId, settingId, Array.Empty<string>());

        var (_, _, userSettings) = await _settingsDbClient.GetSettingsAsync(string.Empty, string.Empty, userId);
        Assert.DoesNotContain(userSettings, s => s.FeatureKey == sectionId && s.FieldKey == settingId);
    }

    [Fact(DisplayName = "GetWorkspaceSettingsAsync возвращает только workspace настройки")]
    public async Task GetWorkspaceSettingsAsync_ShouldReturnWorkspaceSettings()
    {
        var workspaceId = $"ws_{Guid.NewGuid()}";
        var sectionId = "section_ws";
        var settingId = "key_ws";

        await _settingsDbClient.UpsertWorkspaceSettingAsync(workspaceId, sectionId, settingId, "v1");
        await _settingsDbClient.UpsertWorkspaceSettingAsync(workspaceId, sectionId, "key_ws2", "v2");

        var workspaceSettings = await _settingsDbClient.GetWorkspaceSettingsAsync(workspaceId);

        Assert.Equal(2, workspaceSettings.Length);
        Assert.Contains(workspaceSettings, s => s.FieldKey == settingId && s.FieldValue == "v1");
        Assert.Contains(workspaceSettings, s => s.FieldKey == "key_ws2" && s.FieldValue == "v2");
    }

    [Fact(DisplayName = "GetTeamSettingsAsync возвращает только team настройки")]
    public async Task GetTeamSettingsAsync_ShouldReturnTeamSettings()
    {
        var teamId = $"team_{Guid.NewGuid()}";
        var sectionId = "section_team";
        var settingId = "key_team";

        await _settingsDbClient.UpsertTeamSettingAsync(teamId, sectionId, settingId, "v1");
        await _settingsDbClient.UpsertTeamSettingAsync(teamId, sectionId, "key_team2", "v2");

        var teamSettings = await _settingsDbClient.GetTeamSettingsAsync(teamId);

        Assert.Equal(2, teamSettings.Length);
        Assert.Contains(teamSettings, s => s.FieldKey == settingId && s.FieldValue == "v1");
        Assert.Contains(teamSettings, s => s.FieldKey == "key_team2" && s.FieldValue == "v2");
    }

    [Fact(DisplayName = "GetSettingsAsync возвращает настройки по каждому уровню")]
    public async Task GetSettingsAsync_ShouldReturnAllScopes()
    {
        var workspaceId = $"ws_{Guid.NewGuid()}";
        var teamId = $"team_{Guid.NewGuid()}";
        var userId = $"user_{Guid.NewGuid()}";

        await _settingsDbClient.UpsertWorkspaceSettingAsync(workspaceId, "section_ws", "key_ws", "w1");
        await _settingsDbClient.UpsertTeamSettingAsync(teamId, "section_team", "key_team", "t1");
        await _settingsDbClient.UpsertUserSettingAsync(userId, "section_user", "key_user", "u1");

        var (workspaceSettings, teamSettings, userSettings) =
            await _settingsDbClient.GetSettingsAsync(workspaceId, teamId, userId);

        Assert.Contains(workspaceSettings, s => s.FieldKey == "key_ws" && s.FieldValue == "w1");
        Assert.Contains(teamSettings, s => s.FieldKey == "key_team" && s.FieldValue == "t1");
        Assert.Contains(userSettings, s => s.FieldKey == "key_user" && s.FieldValue == "u1");
    }
}
