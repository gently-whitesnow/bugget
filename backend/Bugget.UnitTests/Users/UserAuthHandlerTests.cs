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
/// ответа <c>/_internal/auth</c>. От claim <see cref="AuthClaims.AuthMethod"/> зависит
/// <c>CreatorType.Agent</c>, поэтому проброс проверяется отдельно от остальных заголовков.
/// </summary>
public class UserAuthHandlerTests
{
    private const string UserIdHeaderName = "Auth-Request-User-Id";
    private const string AuthMethodHeaderName = "Auth-Request-Auth-Method";

    [Fact]
    public async Task Пробрасывает_способ_входа_в_claims()
    {
        var context = ContextWithUser();
        context.Request.Headers[AuthMethodHeaderName] = AuthMethods.Pat;

        var result = await AuthenticateAsync(context, AuthMethodHeaderName);

        Assert.True(result.Succeeded);
        Assert.Equal(AuthMethods.Pat, result.Principal!.FindFirst(AuthClaims.AuthMethod)?.Value);
    }

    [Fact]
    public async Task Не_добавляет_способ_входа_когда_заголовка_нет()
    {
        var result = await AuthenticateAsync(ContextWithUser(), AuthMethodHeaderName);

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(AuthClaims.AuthMethod));
    }

    /// <summary>
    /// Заголовок опционален: пока имя не задано в конфигурации, значение из запроса
    /// не должно попадать в identity — иначе клиент назначал бы себе способ входа сам.
    /// </summary>
    [Fact]
    public async Task Игнорирует_способ_входа_когда_имя_заголовка_не_задано()
    {
        var context = ContextWithUser();
        context.Request.Headers[AuthMethodHeaderName] = AuthMethods.Pat;

        var result = await AuthenticateAsync(context, authMethodHeaderName: null);

        Assert.True(result.Succeeded);
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
        string? authMethodHeaderName)
    {
        var schemeOptions = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions
            .Setup(o => o.Get(It.IsAny<string?>()))
            .Returns(new AuthenticationSchemeOptions());

        var headersOptions = new Mock<IOptionsMonitor<AuthHeadersOptions>>();
        headersOptions
            .Setup(o => o.CurrentValue)
            .Returns(new AuthHeadersOptions
            {
                UserIdHeaderName = UserIdHeaderName,
                AuthMethodHeaderName = authMethodHeaderName
            });

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
}
