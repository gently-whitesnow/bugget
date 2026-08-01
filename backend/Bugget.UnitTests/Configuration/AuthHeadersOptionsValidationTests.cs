using Bugget.Api.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bugget.UnitTests.Configuration;

public sealed class AuthHeadersOptionsValidationTests
{
    [Theory]
    [InlineData(null, "X-Team-Id", "OrganizationIdHeaderName")]
    [InlineData("", "X-Team-Id", "OrganizationIdHeaderName")]
    [InlineData(" ", "X-Team-Id", "OrganizationIdHeaderName")]
    [InlineData("X-Workspace-Id", null, "TeamIdHeaderName")]
    [InlineData("X-Workspace-Id", "", "TeamIdHeaderName")]
    [InlineData("X-Workspace-Id", " ", "TeamIdHeaderName")]
    public async Task HostStartFailsWhenRequiredIdentityHeaderNameIsEmpty(
        string? organizationHeader,
        string? teamHeader,
        string invalidOption)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalSettings:Authentication:OrganizationIdHeaderName"] = organizationHeader,
                ["ExternalSettings:Authentication:TeamIdHeaderName"] = teamHeader
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddConfiguration(configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(invalidOption, exception.Failures.Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "X-Team-Id")]
    [InlineData("", "X-Team-Id")]
    [InlineData(" ", "X-Team-Id")]
    [InlineData("X-Workspace-Id", null)]
    [InlineData("X-Workspace-Id", "")]
    [InlineData("X-Workspace-Id", " ")]
    public void WebApplicationDoesNotStartWhenRequiredIdentityHeaderNameIsEmpty(
        string? organizationHeader,
        string? teamHeader)
    {
        using var app = new AuthHeadersApplicationFactory(organizationHeader, teamHeader);

        // Program логирует и поглощает ошибку старта, поэтому WebApplicationFactory видит
        // уже закрытый provider. Парный HostStart-тест выше проверяет точную причину.
        Assert.Throws<ObjectDisposedException>(() => app.CreateClient());
    }

    [Fact]
    public async Task ApplicationStartsWhenDifferentWorkspaceAndTeamIdentityHeaderNamesAreConfigured()
    {
        using var app = new AuthHeadersApplicationFactory("X-Workspace-Id", "X-Team-Id");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/_internal/ping");

        response.EnsureSuccessStatusCode();
    }

    private sealed class AuthHeadersApplicationFactory(string? organizationHeader, string? teamHeader)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("development");
            if (organizationHeader is not null)
            {
                builder.UseSetting(
                    "ExternalSettings:Authentication:OrganizationIdHeaderName",
                    organizationHeader);
            }

            if (teamHeader is not null)
            {
                builder.UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", teamHeader);
            }

            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        }
    }
}
