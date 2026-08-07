using System.Text.Json;
using Bugget.Api.Users.Adapters;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.BackgroundServices;
using Bugget.Api.Users.MattermostOAuth;
using Bugget.Application.Ports;
using Bugget.Application.Users;
using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Application.Users.TeamMembers;
using Bugget.Application.Users.WorkspaceMembers;
using Bugget.Application.Users.Workspaces;
using Bugget.Infrastructure.Users.Avatars;
using Bugget.Infrastructure.Users.DbClients;
using Bugget.Infrastructure.Users.DbUp;
// AuthHeadersOptions в этом файле — тип модуля users, поэтому общий FileStorageOptions
// приходит алиасом, а не импортом namespace.
using FileStorageOptions = Bugget.Application.Options.FileStorageOptions;

namespace Bugget.Api.Users.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Ключ экземпляра файлового хранилища users. Порт и реализация общие с reports,
    /// различается только корень хранилища, поэтому это keyed-регистрация одного типа,
    /// а не второй контракт.
    /// </summary>
    public const string FileStorageServiceKey = "users";

    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthHeadersOptions>(configuration.GetSection("ExternalSettings:Authentication"));
        services.Configure<TeamsOptions>(configuration.GetSection(nameof(TeamsOptions)));
        services.Configure<WorkspacesOptions>(configuration.GetSection(nameof(WorkspacesOptions)));
        // Своя секция: FileStorageOptions в этом же процессе занята хранилищем вложений reports.
        // Тип настроек один, имя — named options: дефолт остаётся за reports.
        services.Configure<FileStorageOptions>(FileStorageServiceKey, configuration.GetSection("UsersFileStorageOptions"));
        services.Configure<SelfHostedOptions>(configuration.GetSection(nameof(SelfHostedOptions)));
        return services;
    }

    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<DbUpService>();
        services.AddSingleton<IUsersDbClient, UsersDbClient>();
        services.AddSingleton<ITeamsDbClient, TeamsDbClient>();
        services.AddSingleton<IWorkspacesDbClient, WorkspacesDbClient>();
        services.AddSingleton<IWorkspaceMembersDbClient, WorkspaceMembersDbClient>();
        services.AddSingleton<ITeamMembersDbClient, TeamMembersDbClient>();
        services.AddSingleton<IMembersDbClient, MembersDbClient>();
        services.AddSingleton<IUserExternalLinksDbClient, UserExternalLinksDbClient>();
        services.AddSingleton<IPersonalAccessTokensDbClient, PersonalAccessTokensDbClient>();

        // Тот же LocalFileStorageClient, что и у reports, но со своим корнем: named options
        // подставляются здесь, в композиционном корне, поэтому прикладной слой о DI не знает.
        services.AddKeyedSingleton<IFileStorageClient>(FileStorageServiceKey, (provider, _) =>
            new Bugget.Infrastructure.Files.LocalFileStorageClient(
                new Microsoft.Extensions.Options.OptionsWrapper<FileStorageOptions>(
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FileStorageOptions>>()
                        .Get(FileStorageServiceKey))));

        return services;
    }

    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration, SelfHostedOptions hostingOptions)
    {
        // TaskQueue
        services.AddSingleton<ITaskQueue, Bugget.Infrastructure.TaskQueue.TaskQueue>();
        services.AddHostedService(sp => (Bugget.Infrastructure.TaskQueue.TaskQueue)sp.GetRequiredService<ITaskQueue>());

        // Avatar service. Файловое хранилище — keyed-экземпляр users.
        services.AddSingleton<IAvatarDownloadService>(provider => new AvatarDownloadService(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredKeyedService<IFileStorageClient>(FileStorageServiceKey),
            provider.GetRequiredService<IUsersDbClient>(),
            provider.GetRequiredService<ILogger<AvatarDownloadService>>()));

        services.AddSingleton<IUsersService, UsersService>();
        services.AddSingleton<ITeamsService, TeamsService>();
        services.AddSingleton<IWorkspacesService, WorkspacesService>();
        services.AddSingleton<ITeamMembersService, TeamMembersService>();
        services.AddSingleton<IWorkspaceMembersService, WorkspaceMembersService>();
        services.AddSingleton<IUserExternalLinksService, UserExternalLinksService>();
        services.AddSingleton<IPersonalAccessTokensService, PersonalAccessTokensService>();

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
