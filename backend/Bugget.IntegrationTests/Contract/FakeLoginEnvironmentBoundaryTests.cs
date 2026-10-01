using System.Net;
using Bugget.Application.Ports;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Граница fake-провайдера входа по окружению. Контроллер попадает в application parts в любом окружении
/// (<c>AddFakeAuth</c> регистрирует только опции), а nginx проксирует <c>/api/authorization/v1/*</c> без
/// <c>auth_request</c>, поэтому единственной защитой маршрута была headers-policy
/// <see cref="Bugget.Api.Authentication.ReportsModuleAuthorizationConvention"/>.
/// </summary>
[Collection("PostgresCollection")]
public sealed class FakeLoginEnvironmentBoundaryTests
{
    [Fact(DisplayName = "GET /v1/fake/login в Production: анонимный вызов не должен выдавать сессию")]
    public async Task FakeLoginIsClosedInProduction()
    {
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousAppDomain = Environment.GetEnvironmentVariable("APP_DOMAIN");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("APP_DOMAIN", "http://localhost");

        try
        {
            using var app = new ProductionWebApplicationFactory();
            var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync(
                $"/v1/fake/login?externalId=attacker-{Guid.NewGuid():N}&name=Attacker");

            var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToArray()
                : [];

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(
                cookies,
                cookie => cookie.StartsWith("access_token=", StringComparison.Ordinal)
                    || cookie.StartsWith("refresh_token=", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("APP_DOMAIN", previousAppDomain);
        }
    }
}
