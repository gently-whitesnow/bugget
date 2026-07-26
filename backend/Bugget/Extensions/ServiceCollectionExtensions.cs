using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Authentication;
using Bugget.BO.DomainEvents;
using Bugget.BO.DomainEvents.Consumer;
using Bugget.BO.DomainEvents.Handlers;
using Bugget.BO.Interfaces;
using Bugget.BO.Services;
using Bugget.BO.Services.Analytics;
using Bugget.BO.Services.Attachments;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Comments;
using Bugget.BO.Services.External;
using Bugget.BO.Services.Idempotency;
using Bugget.BO.Services.Internal;
using Bugget.BO.Services.ReportLinks;
using Bugget.BO.Services.Reports;
using Bugget.BO.Services.Settings;
using Bugget.Configurations;
using Bugget.DA.Files;
using Bugget.DA.Interfaces;
using Bugget.DA.Postgres;
using Bugget.DA.Transactions;
using Bugget.DA.WebSockets;
using Bugget.DbUp;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.Constants;
using Bugget.Entities.Options;
using Bugget.ExternalClients;
using Bugget.HostedServices;
using Bugget.Hubs;
using Bugget.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using OpenTelemetry.Metrics;
using Serilog;
using TaskQueue;

namespace Bugget.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(nameof(FileStorageOptions)));
        services.Configure<OptimizatorSettings>(configuration.GetSection(nameof(OptimizatorSettings)));
        services.Configure<AuthHeadersOptions>(configuration.GetSection("ExternalSettings:Authentication"));
        services.Configure<ReportAliasOptions>(configuration.GetSection(nameof(ReportAliasOptions)));
        services.Configure<DomainEventsConsumerOptions>(configuration.GetSection("DomainEventsConsumer"));
        return services;
    }

    public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((ctx, lc) => lc.ReadFrom.Configuration(configuration));
        return services;
    }

    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services
            .AddSingleton(_ => NpgsqlDataSource.Create(
                Environment.GetEnvironmentVariable(EnvironmentConstants.PostgresConnectionString)
                ?? throw new ApplicationException($"Не задана строка подключения к Postgres, env=[{EnvironmentConstants.PostgresConnectionString}]")))
            .AddSingleton<IReportsDbClient, ReportsDbClient>()
            .AddSingleton<ICommentsDbClient, CommentsDbClient>()
            .AddSingleton<IBugsDbClient, BugsDbClient>()
            .AddSingleton<IReportLinksDbClient, ReportLinksDbClient>()
            .AddSingleton<IAttachmentDbClient, AttachmentDbClient>()
            .AddSingleton<IParticipantsDbClient, ParticipantsDbClient>()
            .AddSingleton<IDomainEventsDbClient, DomainEventsDbClient>()
            .AddSingleton<IDomainEventsCursorClient, DomainEventsCursorClient>()
            .AddSingleton<IDomainEventsConsumerRuntime, DomainEventsConsumerRuntime>()
            .AddSingleton<IReportPhaseIntervalsDbClient, ReportPhaseIntervalsDbClient>()
            .AddSingleton<IAnalyticsDbClient, AnalyticsDbClient>()
            .AddSingleton<IIdempotencyCacheDbClient, IdempotencyCacheDbClient>()
            .AddSingleton<ISettingsDbClient, SettingsDbClient>()
            .AddSingleton<IBugStepsDbClient, BugStepsDbClient>()
            .AddSingleton<IUnitOfWork, NpgsqlUnitOfWork>()
            .AddSingleton<IFileStorageClient, LocalFileStorageClient>();

        // Миграции накатываются в любой среде — так же, как в модуле users.
        services.AddHostedService<DbUpService>();

        return services;
    }

    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services
            .AddSingleton<ReportsService>()
            .AddSingleton<BugsService>()
            .AddSingleton<BugEventsService>()
            .AddSingleton<ReportEventsService>()
            .AddSingleton<ParticipantsService>()
            .AddSingleton<AttachmentOptimizator>()
            .AddSingleton<AttachmentService>()
            .AddSingleton<AttachmentEventsService>()
            .AddSingleton<ImageOptimizeWriter>()
            .AddSingleton<FfmpegService>()
            .AddHostedService<FfmpegWarmupService>()
            .AddSingleton<VideoOptimizeWriter>()
            .AddSingleton<TextOptimizeWriter>()
            .AddSingleton<IAttachmentKeyGenerator, LocalAttachmentKeyGenerator>()
            .AddSingleton<CommentsService>()
            .AddSingleton<CommentEventsService>()
            .AddSingleton<ReportLinksService>()
            .AddSingleton<ReportLinkEventsService>()
            .AddSingleton<LimitsService>()
            .AddSingleton<BugStepsService>()
            .AddSingleton<BugStepEventsService>()
            .AddSingleton<ExternalSearchService>()
            .AddSingleton<ExternalProducerService>()
            .AddSingleton<SettingsService>()
            .AddSingleton<SettingsProcessorProvider>()
            .AddSingleton<CommentLogsService>()
            .AddSingleton<IDomainEventPublisher, DomainEventPublisher>()
            .AddSingleton<IdempotencyCacheService>()
            .AddSingleton<InternalBugsService>()
            .AddSingleton<InternalBugStepsService>()
            .AddSingleton<InternalBugDetailService>()
            .AddSingleton<InternalAttachmentsService>()
            .AddSingleton<InternalCommentsService>()
            .AddSingleton<InternalReportsService>()
            .AddSingleton<InternalDomainEventsService>()
            .AddSingleton<AnalyticsService>()
            .AddSingleton(TimeProvider.System)
            ;

        services.AddHostedService<IdempotencyCacheCleanupService>();

        // T06: локальный outbox-консьюмер. Конкретные handler'ы регистрируются ниже.
        services.AddHostedService<DomainEventsPoller>();

        // T07: handler для ReportStatusChanged → report_phase_intervals (read-model аналитики).
        // Poller диспатчит через ILookup<EventType, IDomainEventHandler> — handler сам
        // объявляет, на какой EventType подписан.
        services.AddSingleton<IDomainEventHandler, ReportPhaseProjectionHandler>();

        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        })
        .AddHubOptions<ReportPageHub>(options => { options.AddFilter<HubExceptionHandlerFilter>(); });

        services.AddSingleton<ITaskQueue, TaskQueue.TaskQueue>()
            .AddHostedService(provider => (TaskQueue.TaskQueue)provider.GetRequiredService<ITaskQueue>());

        return services;
    }

    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.AddExternalClients(configuration);
        services.AddHealthChecks();
        services.AddAuthHeaders();
        services.AddSwaggerConfiguration(configuration);
        services.AddSingleton<ResultExceptionHandlerMiddleware>();
        // Обработчик Result-ошибок модулей users и authorization — они используют Flow.
        services.AddSingleton<Flow.ResultExceptionHandlerMiddleware>();

        services.AddControllers(options =>
        {
            // Модуль reports исторически требует аутентификации на всех своих контроллерах.
            // Контроллеры модулей users и authorization живут в том же процессе и объявляют
            // авторизацию сами ([Auth] / [JwtAuth]), поэтому фильтр вешается по сборке,
            // а не глобально.
            options.Conventions.Add(new ReportsModuleAuthorizationConvention());
        })
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower; })
        .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = _ => new ModelStateInvalidHandler());

        services.AddSingleton<IReportPageHubClient, ReportPageHubClient>();

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusExporter());

        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy", policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddAuthorization(o =>
        {
            // Требовать НЕ дефолтного юзера (т.е. чтобы user_id header был настроен и реально использовался)
            o.AddPolicy(AuthPolicies.RequireUserIdHeader, p =>
                p.RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.UserIdHeaderConfigured, "true")
                .RequireClaim(AuthClaims.UserId, "header"));

            // Требовать team_id именно из header (не через usersClient)
            o.AddPolicy(AuthPolicies.RequireTeamIdHeader, p =>
                p.RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.TeamIdHeaderConfigured, "true")
                .RequireClaim(AuthClaims.TeamId, "header")
                .RequireClaim("team_id"));

            // Требовать organization_id из header
            o.AddPolicy(AuthPolicies.RequireOrganizationIdHeader, p =>
                p.RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.OrganizationIdHeaderConfigured, "true")
                .RequireClaim(AuthClaims.OrganizationId, "header")
                .RequireClaim("organization_id"));
        });

        return services;
    }
}
