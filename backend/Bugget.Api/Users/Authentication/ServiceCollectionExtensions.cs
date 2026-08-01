using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Api.Users.Authentication;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует схему аутентификации модуля users.
    /// DefaultScheme не выставляется: в объединённом процессе им владеет хост.
    /// </summary>
    public static AuthenticationBuilder AddAuthHeaders(this IServiceCollection services) =>
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, UserAuthHandler>(
                AuthSchemeNames.Headers, o => { });
}
