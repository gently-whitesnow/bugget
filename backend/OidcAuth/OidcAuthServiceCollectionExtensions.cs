using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OidcAuth;

public static class OidcAuthServiceCollectionExtensions
{
    public static IServiceCollection AddOidcAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var section = config.GetSection(nameof(OidcAuthOptions));
        services.Configure<OidcAuthOptions>(section);

        services.AddOptions<OidcAuthOptions>()
                .Validate(opts => !string.IsNullOrWhiteSpace(opts.Authority), "Authority is required")
                .ValidateOnStart();

        services.AddSingleton<IOidcTokenValidator, OidcTokenValidator>();

        return services;
    }
}
