using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Authentication;
using Bugget.BO.DomainEvents;
using Bugget.BO.DomainEvents.Consumer;
using Bugget.BO.DomainEvents.Handlers;
using Bugget.BO.Interfaces;
using Bugget.BO.Ports;
using Bugget.BO.Services;
using Bugget.BO.Services.Analytics;
using Bugget.BO.Services.Attachments;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Comments;
using Bugget.BO.Services.External;
using Bugget.BO.Services.ReportLinks;
using Bugget.BO.Services.Reports;
using Bugget.BO.Services.Settings;
using Bugget.Configurations;
using Bugget.DA.Files;
using Bugget.DA.Postgres;
using Bugget.DA.Transactions;
using Bugget.DbUp;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.Constants;
using Bugget.Entities.Options;
using Bugget.ExternalClients;
using Bugget.Hubs;
using Bugget.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
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
        // Профиль оптимизации приходит из external_settings.json: нулевой потолок или
        // невыполнимый бюджет потоков обязан валить старт, а не всплывать OOM'ом (MAIN-194).
        services.AddOptions<OptimizatorSettings>()
            .Bind(configuration.GetSection(nameof(OptimizatorSettings)))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OptimizatorSettings>, OptimizatorSettingsValidator>();
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
            .AddSingleton<IReportPhaseIntervalsDbClient, ReportPhaseIntervalsDbClient>()
            .AddSingleton<IAnalyticsDbClient, AnalyticsDbClient>()
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
            .AddSingleton<VideoOptimizationMetrics>()
            .AddSingleton<VideoTranscodeGate>()
            .AddSingleton<FfmpegProcessRunner>()
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
            .AddSingleton<AnalyticsService>()
            .AddSingleton(TimeProvider.System)
            ;


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
            // Detailed errors отдают клиенту текст любого необработанного исключения в
            // обход HubExceptionHandlerFilter — ровно та утечка, которую граница
            // закрывает (ADR-0008). Причина остаётся в журнале.
            options.EnableDetailedErrors = false;
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
        // services.AddProblemDetails() здесь нет намеренно: IProblemDetailsService строит
        // ответ по правилам ASP.NET, а адаптер границы у нас один — Bugget.Http
        // (ADR-0008). Регистрация была мёртвой: ею никто не пользовался.
        services.AddHealthChecks();
        services.AddAuthHeaders();
        services.AddSwaggerConfiguration(configuration);
        services.AddSingleton<ResultExceptionHandlerMiddleware>();

        services.AddMvcPipeline();

        services.AddSingleton<IReportPageHubClient, ReportPageHubClient>();

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(VideoOptimizationMetrics.MeterName)
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
