using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Api.Authorization.Fake;

public static class FakeAuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds fake authentication for local development.
    /// WARNING: Do not use in production!
    /// </summary>
    public static IServiceCollection AddFakeAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var section = config.GetSection(nameof(FakeAuthOptions));
        services.Configure<FakeAuthOptions>(section);

        return services;
    }
}
