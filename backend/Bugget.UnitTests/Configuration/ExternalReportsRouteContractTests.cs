using System.Text.RegularExpressions;

namespace Bugget.UnitTests.Configuration;

public sealed class ExternalReportsRouteContractTests
{
    [Fact]
    public void NginxAcceptsAndRewritesCanonicalReportLocation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "backend/Bugget.Api/Controllers/ReportsController.cs"));
        var nginx = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "deploy/nginx/snippets/locations/20-app-api.conf"));

        var locationTemplate = Regex.Match(controller, "var location = \\$\"(?<template>[^\"]+)\";")
            .Groups["template"].Value;
        Assert.False(string.IsNullOrEmpty(locationTemplate));
        var externalLocation = locationTemplate
            .Replace("{user.OrganizationId}", "workspace-101", StringComparison.Ordinal)
            .Replace("{user.TeamId}", "team-202", StringComparison.Ordinal)
            .Replace("{contract.Id}", "report-303", StringComparison.Ordinal);

        var locationPattern = Regex.Match(nginx, @"location ~ (?<pattern>\^\S+) \{")
            .Groups["pattern"].Value;
        var rewrite = Regex.Match(nginx, @"rewrite (?<pattern>\^\S+) (?<replacement>\S+) break;");

        Assert.False(string.IsNullOrEmpty(locationPattern));
        Assert.Matches(new Regex(locationPattern, RegexOptions.CultureInvariant), externalLocation);
        Assert.True(rewrite.Success);
        Assert.Equal(
            "/v2/reports/report-303",
            Regex.Replace(
                externalLocation,
                rewrite.Groups["pattern"].Value,
                rewrite.Groups["replacement"].Value,
                RegexOptions.CultureInvariant));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ROOT.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Не найден корень репозитория от {AppContext.BaseDirectory}.");
    }
}
