using System.Net;
using System.Text.Json;
using Bugget.Application.Commands.Report;
using Bugget.Application.ExternalProducer.Context;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Domain;
using Bugget.Domain.Common;
using Bugget.Domain.Reports;
using Bugget.Infrastructure.ExternalClients.Notifications.Mattermost;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Bugget.UnitTests.Services.Reports;

/// <summary>
/// Подпись инициатора в уведомлении о смене ответственного. До kaiten 238350
/// агент никогда не менял ответственного, и этот путь для него не открывался;
/// теперь открывается — и обязан подписываться агентом, а не именем владельца
/// токена (тот же принцип, что в подписи бага, kaiten 237718).
/// </summary>
public sealed class AgentPatchNotificationTests : IDisposable
{
    private const string OwnerUserId = "pat-owner";
    private const string OwnerName = "Владелец Токена";
    private const string ResponsibleUserId = "tester";
    private const string ResponsibleMattermostId = "mm-tester";

    private readonly string? _tokenBefore =
        Environment.GetEnvironmentVariable(MattermostConstants.MattermostBotAccessTokenKey);

    public AgentPatchNotificationTests() =>
        Environment.SetEnvironmentVariable(MattermostConstants.MattermostBotAccessTokenKey, "test-token");

    public void Dispose() =>
        Environment.SetEnvironmentVariable(MattermostConstants.MattermostBotAccessTokenKey, _tokenBefore);

    [Fact]
    public async Task AgentPatch_SignsNotificationAsAgent()
    {
        var (service, usersClient, sentMessages) = BuildSut();

        await service.ExecuteAsync(Context(CreatorType.Agent));

        sentMessages.Should().ContainSingle().Which.Should().Contain("Инициатор: Агент");
        // Имя владельца токена не должно попасть в сообщение даже случайно.
        sentMessages[0].Should().NotContain(OwnerName);
        // И самого владельца незачем разыскивать: подпись агента от него не зависит.
        usersClient.Verify(x => x.GetUserAsync(OwnerUserId), Times.Never);
    }

    [Fact]
    public async Task HumanPatch_SignsNotificationWithTheirName()
    {
        var (service, _, sentMessages) = BuildSut();

        await service.ExecuteAsync(Context(CreatorType.User));

        sentMessages.Should().ContainSingle().Which.Should().Contain($"Инициатор: {OwnerName}");
    }

    private static ReportPatchContext Context(CreatorType actorCreatorType) => new(
        OwnerUserId,
        new ReportPatchDto { Status = (int)ReportStatus.Test, ResponsibleUserId = ResponsibleUserId },
        new ReportPatchResult
        {
            Id = 1,
            PublicId = Guid.NewGuid(),
            Title = "репорт",
            Status = (int)ReportStatus.Test,
            ResponsibleUserId = ResponsibleUserId,
            PastResponsibleUserId = ResponsibleUserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatorTeamId = null,
        },
        actorCreatorType);

    private static (MattermostService Service, Mock<IUsersClient> UsersClient, List<string> SentMessages) BuildSut()
    {
        var usersClient = new Mock<IUsersClient>();
        usersClient.Setup(x => x.GetUserAsync(ResponsibleUserId))
            .Returns(Task.FromResult(MakeUser(ResponsibleUserId, "Тестировщик", ResponsibleMattermostId)));
        usersClient.Setup(x => x.GetUserAsync(OwnerUserId))
            .Returns(Task.FromResult(MakeUser(OwnerUserId, OwnerName, mattermostUserId: null)));

        var sentMessages = new List<string>();
        var handler = new CapturingHandler(sentMessages);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(MattermostConstants.MattermostHttpClientKey))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("https://mattermost.test") });

        var service = new MattermostService(
            usersClient.Object,
            new MattermostClient(httpClientFactory.Object),
            Options.Create(new ReportAliasOptions { AliasMode = ReportAliasMode.Default }));

        return (service, usersClient, sentMessages);
    }

    private static User MakeUser(string id, string name, string? mattermostUserId) => new()
    {
        Id = id,
        Name = name,
        MattermostUserId = mattermostUserId,
    };

    /// <summary>
    /// Отвечает на три запроса, которые делает <see cref="MattermostClient"/>
    /// (кто я → личный канал → пост), и запоминает текст отправленного сообщения.
    /// </summary>
    private sealed class CapturingHandler(List<string> sentMessages) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/posts"))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                sentMessages.Add(JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"stub"}""", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
