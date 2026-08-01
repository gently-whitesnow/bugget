using System.Text.Json;
using System.Text.Json.Serialization;
using Bugget.Api.Authentication;
using Bugget.Api.Configurations;
using Bugget.Api.Hubs;
using Bugget.Api.Middlewares;
using Bugget.Application.DomainEvents;
using Bugget.Application.DomainEvents.Consumer;
using Bugget.Application.DomainEvents.Handlers;
using Bugget.Application.Interfaces;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services;
using Bugget.Application.Services.Analytics;
using Bugget.Application.Services.Attachments;
using Bugget.Application.Services.Bugs;
using Bugget.Application.Services.Comments;
using Bugget.Application.Services.External;
using Bugget.Application.Services.ReportLinks;
using Bugget.Application.Services.Reports;
using Bugget.Application.Services.Settings;
using Bugget.Domain.Authentication;
using Bugget.Domain.Constants;
using Bugget.Infrastructure.Attachments;
using Bugget.Infrastructure.DbUp;
using Bugget.Infrastructure.ExternalClients;
using Bugget.Infrastructure.Files;
using Bugget.Infrastructure.Postgres;
using Bugget.Infrastructure.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using OpenTelemetry.Metrics;
using Serilog;

namespace Bugget.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(nameof(FileStorageOptions)));
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

        // Пережатие вложений и определение mime: реализации портов и всё, что знает
        // про ImageSharp, ffmpeg и libmagic, регистрирует сама инфраструктура.
        services.AddAttachmentOptimization(configuration);

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

        services.AddSingleton<ITaskQueue, Bugget.Infrastructure.TaskQueue.TaskQueue>()
            .AddHostedService(provider => (Bugget.Infrastructure.TaskQueue.TaskQueue)provider.GetRequiredService<ITaskQueue>());

        return services;
    }

    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.AddExternalClients(configuration);
        // services.AddProblemDetails() здесь нет намеренно: IProblemDetailsService строит
        // ответ по правилам ASP.NET, а адаптер границы у нас один — Bugget.Api.Http
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
