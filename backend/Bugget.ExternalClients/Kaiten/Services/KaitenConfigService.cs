using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.Settings;

namespace Bugget.ExternalClients.Kaiten.Services;

public sealed class KaitenConfigService(
    ISettingsDbClient settingsDbClient)
{

    public async Task<(bool useReportLinking, bool sendReportLinkToComments)> GetTeamSettingsAsync(string teamId)
    {
        var teamSettings = await settingsDbClient.GetTeamSettingsAsync(teamId);

        var kaitenSettings = teamSettings
            .Where(s => s.FeatureKey == KaitenConstants.FeatureKey)
            .ToArray();

        var useReportLinkingRaw = kaitenSettings
            .FirstOrDefault(s => s.FieldKey == KaitenConstants.UseReportLinkingFieldKey)?.FieldValue;
        var useReportLinking = string.IsNullOrEmpty(useReportLinkingRaw) || bool.TryParse(useReportLinkingRaw, out var ul) && ul;

        var sendReportLinkToCommentsRaw = kaitenSettings
            .FirstOrDefault(s => s.FieldKey == KaitenConstants.SendReportLinkToCommentsFieldKey)?.FieldValue;
        var sendReportLinkToComments = string.IsNullOrEmpty(sendReportLinkToCommentsRaw) || bool.TryParse(sendReportLinkToCommentsRaw, out var sc) && sc;

        return (useReportLinking, sendReportLinkToComments);
    }

    public async Task<KaitenWorkspaceConfig?> GetWorkspaceConfigAsync(string workspaceId)
    {
        var settings = await settingsDbClient.GetWorkspaceSettingsAsync(workspaceId);
        return ExtractConfig(settings);
    }

    private static KaitenWorkspaceConfig? ExtractConfig(WorkspaceSettingDbModel[] settings)
    {
        var kaitenSettings = settings.Where(s => s.FeatureKey == KaitenConstants.FeatureKey).ToArray();

        var domain = kaitenSettings.FirstOrDefault(s => s.FieldKey == KaitenConstants.DomainFieldKey)?.FieldValue;
        var accessToken = kaitenSettings.FirstOrDefault(s => s.FieldKey == KaitenConstants.AccessTokenFieldKey)?.FieldValue;

        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        return new KaitenWorkspaceConfig(domain, accessToken);
    }
}
