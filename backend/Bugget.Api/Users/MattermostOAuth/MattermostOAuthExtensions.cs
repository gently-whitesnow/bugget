using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Api.Users.MattermostOAuth;

public static class MattermostOAuthExtensions
{
    public static IServiceCollection AddMattermostOAuth(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(nameof(MattermostOAuthOptions));
        services.Configure<MattermostOAuthOptions>(section);

        services.AddHttpClient("MattermostOAuth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<MattermostOAuthClient>();

        return services;
    }
}
