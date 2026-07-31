using Bugget.BO.Errors;
using Bugget.Entities.Errors;

namespace Bugget.BO.Services.Settings;

public sealed class SettingsProcessorProvider(
    IEnumerable<IWorkspaceSettingsProcessor> workspaceSettingsProcessors,
    IEnumerable<ITeamSettingsProcessor> teamSettingsProcessors,
    IEnumerable<IUserSettingsProcessor> userSettingsProcessors)
{
    public (ITeamSettingsProcessor? Value, Error? Error) GetTeamSettingsProcessor(string sectionId)
    {
        var processor = teamSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return (null, BoErrors.TeamSettingsProcessorNotFound);
        }

        return (processor, null);
    }

    public (IWorkspaceSettingsProcessor? Value, Error? Error) GetWorkspaceSettingsProcessor(string sectionId)
    {
        var processor = workspaceSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return (null, BoErrors.WorkspaceSettingsProcessorNotFound);
        }

        return (processor, null);
    }

    public (IUserSettingsProcessor? Value, Error? Error) GetUserSettingsProcessor(string sectionId)
    {
        var processor = userSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return (null, BoErrors.UserSettingsProcessorNotFound);
        }

        return (processor, null);
    }

    public (IEnumerable<IWorkspaceSettingsProcessor>, IEnumerable<ITeamSettingsProcessor>, IEnumerable<IUserSettingsProcessor>) GetSettingsProcessors()
    {
        return (workspaceSettingsProcessors, teamSettingsProcessors, userSettingsProcessors);
    }
}
