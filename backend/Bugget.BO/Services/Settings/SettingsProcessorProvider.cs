using Bugget.BO.Errors;
using Monade;

namespace Bugget.BO.Services.Settings;

public sealed class SettingsProcessorProvider(
    IEnumerable<IWorkspaceSettingsProcessor> workspaceSettingsProcessors,
    IEnumerable<ITeamSettingsProcessor> teamSettingsProcessors,
    IEnumerable<IUserSettingsProcessor> userSettingsProcessors)
{
    public MonadeStruct<ITeamSettingsProcessor> GetTeamSettingsProcessor(string sectionId)
    {
        var processor = teamSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return BoErrors.TeamSettingsProcessorNotFound;
        }

        return new MonadeStruct<ITeamSettingsProcessor>(processor);
    }

    public MonadeStruct<IWorkspaceSettingsProcessor> GetWorkspaceSettingsProcessor(string sectionId)
    {
        var processor = workspaceSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return BoErrors.WorkspaceSettingsProcessorNotFound;
        }

        return new MonadeStruct<IWorkspaceSettingsProcessor>(processor);
    }

    public MonadeStruct<IUserSettingsProcessor> GetUserSettingsProcessor(string sectionId)
    {
        var processor = userSettingsProcessors.FirstOrDefault(p => p.SectionId == sectionId);
        if (processor == null)
        {
            return BoErrors.UserSettingsProcessorNotFound;
        }

        return new MonadeStruct<IUserSettingsProcessor>(processor);
    }

    public (IEnumerable<IWorkspaceSettingsProcessor>, IEnumerable<ITeamSettingsProcessor>, IEnumerable<IUserSettingsProcessor>) GetSettingsProcessors()
    {
        return (workspaceSettingsProcessors, teamSettingsProcessors, userSettingsProcessors);
    }
}
