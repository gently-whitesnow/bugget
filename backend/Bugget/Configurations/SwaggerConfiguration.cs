using System.Reflection;
using Bugget.Entities.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Bugget.Configurations;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            // По документу на модуль: контроллеры трёх модулей живут в одном процессе,
            // общий список эндпоинтов был бы нечитаемым.
            c.SwaggerDoc(ReportsDoc, new OpenApiInfo { Title = "Report API", Version = "v1" });
            c.SwaggerDoc(UsersDoc, new OpenApiInfo { Title = "Users API", Version = "v1" });
            c.SwaggerDoc(AuthorizationDoc, new OpenApiInfo { Title = "Authorization API", Version = "v1" });

            c.CustomSchemaIds(type => type.FullName);
            c.DocInclusionPredicate((docName, api) =>
                api.ActionDescriptor is ControllerActionDescriptor descriptor
                && ResolveDoc(descriptor.ControllerTypeInfo.Assembly) == docName);

            foreach (var assembly in new[]
                     {
                         typeof(SwaggerConfiguration).Assembly,
                         typeof(Users.Api.Controllers.ApiController).Assembly,
                         typeof(Authorization.Api.AuthorizationSchemeNames).Assembly,
                     })
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            }

            var authHeaders = configuration.GetSection("ExternalSettings:Authentication").Get<AuthHeadersOptions>();
            if (authHeaders != null)
            {
                if (!string.IsNullOrEmpty(authHeaders.UserIdHeaderName))
                {
                    c.AddSecurityDefinition("UserId", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = authHeaders.UserIdHeaderName,
                        Description = "User ID header"
                    });
                }

                if (!string.IsNullOrEmpty(authHeaders.TeamIdHeaderName))
                {
                    c.AddSecurityDefinition("TeamId", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = authHeaders.TeamIdHeaderName,
                        Description = "Team ID header"
                    });
                }

                if (!string.IsNullOrEmpty(authHeaders.OrganizationIdHeaderName))
                {
                    c.AddSecurityDefinition("OrganizationId", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = authHeaders.OrganizationIdHeaderName,
                        Description = "Organization ID header"
                    });
                }

                var securityRequirements = new List<OpenApiSecurityRequirement>();

                if (!string.IsNullOrEmpty(authHeaders.UserIdHeaderName))
                {
                    securityRequirements.Add(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "UserId" }
                            },
                            Array.Empty<string>()
                        }
                    });
                }

                if (!string.IsNullOrEmpty(authHeaders.TeamIdHeaderName))
                {
                    securityRequirements.Add(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "TeamId" }
                            },
                            Array.Empty<string>()
                        }
                    });
                }

                if (!string.IsNullOrEmpty(authHeaders.OrganizationIdHeaderName))
                {
                    securityRequirements.Add(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "OrganizationId" }
                            },
                            Array.Empty<string>()
                        }
                    });
                }

                foreach (var requirement in securityRequirements)
                {
                    c.AddSecurityRequirement(requirement);
                }
            }
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
    {
        app.UseSwagger(c => c.RouteTemplate = "_internal/swagger/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint($"{ReportsDoc}/swagger.json", "Report API v1");
            c.SwaggerEndpoint($"{UsersDoc}/swagger.json", "Users API v1");
            c.SwaggerEndpoint($"{AuthorizationDoc}/swagger.json", "Authorization API v1");
            c.RoutePrefix = "_internal/swagger";
        });

        return app;
    }

    private const string ReportsDoc = "v1";
    private const string UsersDoc = "users";
    private const string AuthorizationDoc = "authorization";

    private static string ResolveDoc(Assembly assembly)
    {
        if (assembly == typeof(Users.Api.Controllers.ApiController).Assembly
            || assembly == typeof(MattermostOAuth.MattermostOAuthController).Assembly)
        {
            return UsersDoc;
        }

        if (assembly == typeof(Authorization.Api.AuthorizationSchemeNames).Assembly
            || assembly == typeof(OidcAuth.OidcController).Assembly
            || assembly == typeof(FakeAuth.FakeController).Assembly)
        {
            return AuthorizationDoc;
        }

        return ReportsDoc;
    }
}
