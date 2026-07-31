using System.Text.Json;
using Authentication;
using MattermostOAuth;
using TaskQueue;
using Users.Api.Adapters;
using Users.Api.BackgroundServices;
using Users.BO;
using Users.BO.Avatars;
using Users.BO.Interfaces;
using Users.BO.Ports;
using Users.BO.TeamMembers;
using Users.BO.WorkspaceMembers;
using Users.BO.Workspaces;
using Users.DA.DbClients;
using Users.DA.Files;
using Users.DbUp;
using Users.Entities.Options;

namespace Users.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthHeadersOptions>(configuration.GetSection("ExternalSettings:Authentication"));
        services.Configure<TeamsOptions>(configuration.GetSection(nameof(TeamsOptions)));
        services.Configure<WorkspacesOptions>(configuration.GetSection(nameof(WorkspacesOptions)));
        // Своя секция: FileStorageOptions в этом же процессе занята хранилищем вложений reports.
        services.Configure<FileStorageOptions>(configuration.GetSection("UsersFileStorageOptions"));
        services.Configure<SelfHostedOptions>(configuration.GetSection(nameof(SelfHostedOptions)));
        return services;
    }

    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<DbUpService>();
        services.AddSingleton<IUsersRepository, UsersDbClient>();
        services.AddSingleton<ITeamsRepository, TeamsDbClient>();
        services.AddSingleton<IWorkspacesRepository, WorkspacesDbClient>();
        services.AddSingleton<IWorkspaceMembersRepository, WorkspaceMembersDbClient>();
        services.AddSingleton<ITeamMembersRepository, TeamMembersDbClient>();
        services.AddSingleton<IMembersRepository, MembersDbClient>();
        services.AddSingleton<IUserExternalLinksRepository, UserExternalLinksDbClient>();
        services.AddSingleton<IFileStorageClient, LocalFileStorageClient>();

        return services;
    }

    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration, SelfHostedOptions hostingOptions)
    {
        // TaskQueue
        services.AddSingleton<ITaskQueue, TaskQueue.TaskQueue>();
        services.AddHostedService(sp => (TaskQueue.TaskQueue)sp.GetRequiredService<ITaskQueue>());

        // Avatar service
        services.AddSingleton<IAvatarDownloadService, AvatarDownloadService>();

        services.AddSingleton<IUsersService, UsersService>();
        services.AddSingleton<ITeamsService, TeamsService>();
        services.AddSingleton<IWorkspacesService, WorkspacesService>();
        services.AddSingleton<ITeamMembersService, TeamMembersService>();
        services.AddSingleton<IWorkspaceMembersService, WorkspaceMembersService>();
        services.AddSingleton<IUserExternalLinksService, UserExternalLinksService>();

        if (hostingOptions.Enabled)
        {
            services.AddHostedService<WorkspaceInitializationService>();
        }

        var mmOptions = configuration.GetSection(nameof(MattermostOAuthOptions)).Get<MattermostOAuthOptions>();
        if (mmOptions is not null && mmOptions.Enabled)
        {
            services.AddMattermostOAuth(configuration);
            services.AddSingleton<IMattermostUserUpdater, MattermostUserUpdaterAdapter>();
        }

        var mmBotOptions = configuration.GetSection(nameof(MattermostBotOptions)).Get<MattermostBotOptions>();
        if (mmBotOptions is not null && mmBotOptions.Enabled)
        {
            services.Configure<MattermostBotOptions>(configuration.GetSection(nameof(MattermostBotOptions)));
            services.AddHttpClient("MattermostBot", client => { client.Timeout = TimeSpan.FromSeconds(30); });
            services.AddHostedService<MattermostBotListener>();
        }

        return services;
    }

    /// <summary>
    /// Web-часть модуля. Controllers, CORS, JSON-настройки и pipeline принадлежат хосту —
    /// здесь только то, что специфично для users.
    /// </summary>
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.AddAuthHeaders();

        services.AddHttpClient("AvatarDownload", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
