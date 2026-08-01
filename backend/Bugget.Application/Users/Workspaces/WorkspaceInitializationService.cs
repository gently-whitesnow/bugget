using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users.Workspaces;

public class WorkspaceInitializationService(
    IOptions<SelfHostedOptions> options,
    ILogger<WorkspaceInitializationService> logger,
    IWorkspacesService workspacesService,
    ITeamsRepository teamsRepository) : IHostedService
{

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var existingWorkspaces = await workspacesService.ListWorkspacesAsync();
        if (existingWorkspaces.Length > 0)
        {
            logger.LogInformation("Default workspace already exists, checking teams");
            await EnsureDefaultTeamAsync(existingWorkspaces[0].Id);
            return;
        }

        var workspace = await workspacesService.InternalCreateWorkspaceAsync(options.Value.DefaultWorkspaceName);
        logger.LogInformation("Default workspace created: {WorkspaceName}", options.Value.DefaultWorkspaceName);

        await EnsureDefaultTeamAsync(workspace.Id);
    }

    private async Task EnsureDefaultTeamAsync(int workspaceId)
    {
        var teams = await teamsRepository.ListTeamsAsync([workspaceId]);
        if (teams.Length > 0)
        {
            logger.LogInformation("Teams already exist in workspace {WorkspaceId}", workspaceId);
            return;
        }

        await teamsRepository.CreateTeamAsync(workspaceId, options.Value.DefaultTeamName);
        logger.LogInformation("Default team created: {TeamName}", options.Value.DefaultTeamName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
