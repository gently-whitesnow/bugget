using Bugget.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task HostStartsWhenWorkspaceAndTeamIdentityHeaderNamesAreConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalSettings:Authentication:OrganizationIdHeaderName"] = "X-Workspace-Id",
                ["ExternalSettings:Authentication:TeamIdHeaderName"] = "X-Team-Id"
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddConfiguration(configuration))
            .Build();

        await host.StartAsync();
        await host.StopAsync();
    }
}
