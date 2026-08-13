using System.Text.Json;
using System.Text.RegularExpressions;
using Bugget.Api.Authorization.Extensions;

namespace Bugget.UnitTests.Configuration;

/// <summary>
/// Атрибуция PAT-действий как agent держится на одном имени заголовка в трёх местах:
/// PAT-схема пишет его в ответ auth_request (<see cref="HttpContextExtensions.AuthMethodHeaderName"/>),
/// nginx переносит в запрос к приложению (auth-proxy-response.conf), приложение читает
/// его по имени из ExternalSettings:Authentication:AuthMethodHeaderName. Выпадение
/// любого конца молча деградирует creator_type к user — так уже случилось в проде,
/// когда external_settings развёртывания потерял ключ (bugget report 436, баг 2).
/// Тест держит эталонные конфиги репозитория согласованными с кодом.
/// </summary>
public sealed class AuthMethodHeaderContractTests
{
    [Fact(DisplayName =
        "Эталонный external_settings объявляет имя заголовка способа входа, " +
        "совпадающее с тем, которое PAT-схема пишет в ответ auth_request")]
    public void ExternalSettings_ShouldDeclareAuthMethodHeaderName_MatchingPatSchemeConstant()
    {
        var externalSettingsPath = Path.Combine(
            FindRepositoryRoot(),
            "deploy/external-settings/bugget-api/external_settings.json");

        using var settings = JsonDocument.Parse(File.ReadAllText(externalSettingsPath));
        var authentication = settings.RootElement
            .GetProperty("ExternalSettings")
            .GetProperty("Authentication");

        Assert.True(
            authentication.TryGetProperty("AuthMethodHeaderName", out var headerName),
            "В ExternalSettings:Authentication нет AuthMethodHeaderName: claim auth_method "
            + "не создастся, и действия по PAT будут записаны как creator_type=user.");
        Assert.Equal(HttpContextExtensions.AuthMethodHeaderName, headerName.GetString());
    }

    [Fact(DisplayName =
        "nginx переносит заголовок способа входа из ответа auth_request в запрос к приложению")]
    public void AuthProxyResponseConf_ShouldForwardAuthMethodHeader_FromAuthSubrequestToApp()
    {
        var nginx = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "deploy/nginx/snippets/includes/auth-proxy-response.conf"));

        // proxy_set_header <имя-заголовка> $<переменная>;
        var forward = Regex.Match(
            nginx,
            $@"proxy_set_header\s+{Regex.Escape(HttpContextExtensions.AuthMethodHeaderName)}\s+\$(?<variable>\w+)\s*;",
            RegexOptions.CultureInvariant);
        Assert.True(
            forward.Success,
            $"auth-proxy-response.conf не пробрасывает {HttpContextExtensions.AuthMethodHeaderName} "
            + "в запрос к приложению — атрибуция PAT-действий деградирует к user.");

        // auth_request_set $<переменная> $upstream_http_<имя-заголовка в snake_case>;
        var upstreamVariable = "upstream_http_"
            + HttpContextExtensions.AuthMethodHeaderName.ToLowerInvariant().Replace('-', '_');
        Assert.Matches(
            new Regex(
                $@"auth_request_set\s+\${forward.Groups["variable"].Value}\s+\${Regex.Escape(upstreamVariable)}\s*;",
                RegexOptions.CultureInvariant),
            nginx);
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
