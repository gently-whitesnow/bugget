using System.Security.Claims;
using System.Text.Encodings.Web;
using Bugget.Api.Users.Authentication;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Users;

/// <summary>
/// Header-схема модуля users: сюда приходят <c>Auth-Request-*</c>, которые nginx взял из
/// ответа <c>/_internal/auth</c>. Схема — единственное место, где эти заголовки становятся
/// identity, поэтому проверяется и состав claims, и отказ, и реакция на незаданные имена.
/// </summary>
public class UserAuthHandlerTests
{
    private const string UserIdHeaderName = "Auth-Request-User-Id";
    private const string TeamIdHeaderName = "Auth-Request-Team-Id";
    private const string WorkspaceIdHeaderName = "Auth-Request-Workspace-Id";
    private const string WorkspaceRoleHeaderName = "Auth-Request-Workspace-Role";
    private const string AuthMethodHeaderName = "Auth-Request-Auth-Method";

    [Fact]
    public async Task Собирает_identity_из_всех_заголовков()
    {
        var context = ContextWithUser();
        context.Request.Headers[TeamIdHeaderName] = "7";
        context.Request.Headers[WorkspaceIdHeaderName] = "3";
        context.Request.Headers[WorkspaceRoleHeaderName] = WorkspaceRole.Admin;

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        var claims = result.Principal!;
        Assert.Equal("42", claims.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("7", claims.FindFirst(ClaimKey.Team)?.Value);
        Assert.Equal("3", claims.FindFirst(ClaimKey.Workspace)?.Value);
        Assert.Equal(WorkspaceRole.Admin, claims.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public async Task Отказывает_когда_идентификатор_пользователя_не_пришёл()
    {
        var result = await AuthenticateAsync(new DefaultHttpContext());

        Assert.False(result.Succeeded);
        Assert.Equal("User ID not found", result.Failure?.Message);
    }

    /// <summary>
    /// nginx подставляет пустую строку, когда в ответе <c>/_internal/auth</c> заголовка не было,
    /// поэтому пробельный идентификатор — достижимое состояние, а не только опечатка в тесте.
    /// </summary>
    [Fact]
    public async Task Отказывает_когда_идентификатор_пользователя_из_пробелов()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[UserIdHeaderName] = "   ";

        var result = await AuthenticateAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal("User ID not found", result.Failure?.Message);
    }

    [Fact]
    public async Task Не_добавляет_claims_команды_и_пространства_когда_заголовков_нет()
    {
        var result = await AuthenticateAsync(ContextWithUser());

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(ClaimKey.Team));
        Assert.Null(result.Principal!.FindFirst(ClaimKey.Workspace));
    }

    /// <summary>
    /// Роль попадает в ответ <c>/_internal/auth</c> только вместе с найденным workspace, так что
    /// её отсутствие штатно. Member здесь — не повышение прав: доступ к админским ручкам даёт
    /// только <see cref="WorkspaceRole.Admin"/>.
    /// </summary>
    [Fact]
    public async Task Подставляет_member_когда_роль_не_пришла()
    {
        var result = await AuthenticateAsync(ContextWithUser());

        Assert.True(result.Succeeded);
        Assert.Equal(WorkspaceRole.Member, result.Principal!.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public async Task Пробрасывает_способ_входа_в_claims()
    {
        var context = ContextWithUser();
        context.Request.Headers[AuthMethodHeaderName] = AuthMethods.Pat;

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(AuthMethods.Pat, result.Principal!.FindFirst(AuthClaims.AuthMethod)?.Value);
    }

    [Fact]
    public async Task Не_добавляет_способ_входа_когда_заголовка_нет()
    {
        var result = await AuthenticateAsync(ContextWithUser());

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(AuthClaims.AuthMethod));
    }

    /// <summary>
    /// Имена заголовков приходят из конфигурации, и незаданное имя означает «эту часть identity
    /// мы не принимаем». Иначе клиент назначал бы себе команду, пространство и способ входа сам.
    /// </summary>
    [Fact]
    public async Task Игнорирует_заголовки_имена_которых_не_заданы()
    {
        var context = ContextWithUser();
        context.Request.Headers[TeamIdHeaderName] = "7";
        context.Request.Headers[WorkspaceIdHeaderName] = "3";
        context.Request.Headers[AuthMethodHeaderName] = AuthMethods.Pat;

        var result = await AuthenticateAsync(context, new AuthHeadersOptions
        {
            UserIdHeaderName = UserIdHeaderName
        });

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(ClaimKey.Team));
        Assert.Null(result.Principal!.FindFirst(ClaimKey.Workspace));
        Assert.Null(result.Principal!.FindFirst(AuthClaims.AuthMethod));
    }

    private static DefaultHttpContext ContextWithUser()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[UserIdHeaderName] = "42";
        return context;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        DefaultHttpContext context,
        AuthHeadersOptions? options = null)
    {
        var schemeOptions = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions
            .Setup(o => o.Get(It.IsAny<string?>()))
            .Returns(new AuthenticationSchemeOptions());

        var headersOptions = new Mock<IOptionsMonitor<AuthHeadersOptions>>();
        headersOptions
            .Setup(o => o.CurrentValue)
            .Returns(options ?? AllHeadersConfigured());

        var handler = new UserAuthHandler(
            schemeOptions.Object,
            LoggerFactory.Create(_ => { }),
            UrlEncoder.Default,
            headersOptions.Object);

        await handler.InitializeAsync(
            new AuthenticationScheme("headers", null, typeof(UserAuthHandler)),
            context);

        return await handler.AuthenticateAsync();
    }

    private static AuthHeadersOptions AllHeadersConfigured() => new()
    {
        UserIdHeaderName = UserIdHeaderName,
        TeamIdHeaderName = TeamIdHeaderName,
        WorkspaceIdHeaderName = WorkspaceIdHeaderName,
        WorkspaceRoleHeaderName = WorkspaceRoleHeaderName,
        AuthMethodHeaderName = AuthMethodHeaderName
    };
}
