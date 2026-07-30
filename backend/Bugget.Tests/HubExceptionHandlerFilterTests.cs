using System.Security.Claims;
using System.Text.Json;
using Bugget.BO.Errors;
using Bugget.Extensions;
using Bugget.Hubs;
using Bugget.Middlewares;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bugget.Tests;

/// <summary>
/// Граница realtime-канала закрыта по умолчанию: наружу проходит только payload,
/// собранный адаптером. Проверяется поведение самого фильтра, а не соглашение о том,
/// как его зовут: сырой <see cref="HubException"/> приходит сюда так же, как из чужого
/// кода или библиотеки, и его сообщение SignalR отдал бы клиенту дословно.
/// </summary>
public sealed class HubExceptionHandlerFilterTests
{
    private const string Secret = "Npgsql connection to 10.0.0.7 failed: password=hunter2";

    [Fact]
    public async Task Trusted_descriptor_reaches_the_client_unchanged()
    {
        var descriptor = BoErrors.ReportNotFoundError.ToDescriptor();

        var thrown = await InvokeAsync(() => throw new RealtimeProblemException(descriptor));

        Assert.IsType<RealtimeProblemException>(thrown);
        Assert.Equal(new RealtimeProblemException(descriptor).Message, thrown.Message);
        Assert.Equal("report_not_found", CodeOf(thrown));
    }

    [Fact]
    public async Task Raw_hub_exception_is_sanitized_instead_of_being_forwarded()
    {
        var thrown = await InvokeAsync(() => throw new HubException(Secret));

        Assert.IsType<RealtimeProblemException>(thrown);
        Assert.DoesNotContain(Secret, thrown.Message, StringComparison.Ordinal);
        Assert.Equal("internal_server_error", CodeOf(thrown));
    }

    [Fact]
    public async Task Ordinary_exception_does_not_publish_its_message()
    {
        var thrown = await InvokeAsync(() => throw new InvalidOperationException(Secret));

        Assert.IsType<RealtimeProblemException>(thrown);
        Assert.DoesNotContain(Secret, thrown.Message, StringComparison.Ordinal);
        Assert.Equal("internal_server_error", CodeOf(thrown));
    }

    private static string? CodeOf(Exception exception)
    {
        using var document = JsonDocument.Parse(exception.Message);
        return document.RootElement.GetProperty("code").GetString();
    }

    private static async Task<Exception> InvokeAsync(Func<object?> hubMethod)
    {
        var filter = new HubExceptionHandlerFilter(NullLogger<HubExceptionHandlerFilter>.Instance);

        return await Assert.ThrowsAnyAsync<Exception>(async () =>
            await filter.InvokeMethodAsync(HubInvocation(), _ => ValueTask.FromResult(hubMethod())));
    }

    private static HubInvocationContext HubInvocation() =>
        new(new StubCallerContext(),
            new ServiceCollection().BuildServiceProvider(),
            new StubHub(),
            typeof(StubHub).GetMethod(nameof(StubHub.DoAsync))!,
            []);

    private sealed class StubHub : Hub
    {
        public Task DoAsync() => Task.CompletedTask;
    }

    private sealed class StubCallerContext : HubCallerContext
    {
        public override string ConnectionId => "test-connection";

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
