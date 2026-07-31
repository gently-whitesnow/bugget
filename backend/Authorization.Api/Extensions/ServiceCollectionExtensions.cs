using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Authentication;
using Authorization.Abstractions;
using Authorization.Api.DbClients;
using Authorization.Api.Interfaces;
using Authorization.Api.Services;
using Authorization.Interfaces;
using Authorization.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OidcAuth;
using StackExchange.Redis;
using Users.Entities.Errors;

namespace Authorization.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthHeadersOptions>(configuration.GetSection("ExternalSettings:Authentication"));
        services.Configure<UserCacheOptions>(configuration.GetSection(nameof(UserCacheOptions)));
        return services;
    }

    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable(Constants.RedisConnectionStringEnv)
                                                                            ?? throw new ApplicationException($"Не задана строка подключения к Redis, env=[{Constants.RedisConnectionStringEnv}]")));

        services.AddSingleton<IRefreshRevocationStore, TokenRevocationRedisClient>();
        services.AddSingleton<IRefreshRotationCache, RefreshRotationRedisCache>();
        services.AddSingleton<IUserCache, UserCacheRedisClient>();
        return services;
    }

    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddSingleton<IUsersService, UsersService>();
        services.AddSingleton<IRedirectService, RedirectService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<JwtOptions>(cfg.GetRequiredSection(nameof(JwtOptions)));
        services.Configure<KeyStoreOptions>(cfg.GetRequiredSection(nameof(KeyStoreOptions)));

        var keyStore = cfg.GetRequiredSection(nameof(KeyStoreOptions)).Get<KeyStoreOptions>()!;
        var pairs = RsaKeyPairsProvider
            .LoadOrCreateAsync(keyStore.PemFilePath).GetAwaiter().GetResult();

        services.AddSingleton<IRsaPrivateKeyStorage>
            (_ => RsaPrivateKeyRepository.FromPairs(pairs));
        services.AddSingleton<IJwkSetStorage>
            (_ => JwkSetRepository.FromRsaKeyPairs(pairs));

        services.AddSingleton<ITokensService, TokensService>();
        services.AddSingleton<IExternalAuthService, ExternalAuthService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<JwtOptions>>().Value;
            var jwks = sp.GetRequiredService<IJwkSetStorage>();
            // Используем первый доступный ключ вместо жестко заданного "access"
            var jwkSet = jwks.GetJWKSetAsync().GetAwaiter().GetResult();
            var jwk = jwkSet.Keys.First();
            if (!JsonWebKeyConverter.TryConvertToSecurityKey(jwk, out var key))
            {
                throw new InvalidOperationException("Bad access JWK");
            }

            return new TokenValidationParameters
            {
                ValidIssuer = opts.Issuer,
                ValidAudience = opts.Audience,
                IssuerSigningKey = key,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };
        });

        // Схема всегда одна и та же (AuthorizationSchemeNames.Jwt) — меняется только
        // конфигуратор. Иначе контроллерам пришлось бы знать про режим OIDC.
        services.AddAuthentication().AddJwtBearer(AuthorizationSchemeNames.Jwt, _ => { });

        var oidcOptions = cfg.GetSection(nameof(OidcAuthOptions)).Get<OidcAuthOptions>();
        if (oidcOptions?.Enabled == true)
        {
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureOidcJwtBearerOptions>();
        }
        else
        {
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        }

        return services;
    }

    /// <summary>
    /// Web-часть модуля. Controllers, CORS, JSON-настройки и pipeline принадлежат хосту —
    /// здесь только то, что специфично для authorization.
    /// </summary>
    public static IServiceCollection AddWebApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<NotFoundExceptionMiddleware>();

        return services;
    }
}
