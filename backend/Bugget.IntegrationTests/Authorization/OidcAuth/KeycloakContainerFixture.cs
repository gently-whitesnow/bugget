using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Oidc;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Xunit.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Bugget.IntegrationTests.Authorization.OidcAuth;

/// <summary>
/// Shared Keycloak container fixture for all tests.
/// </summary>
public class KeycloakContainerFixture : IAsyncLifetime
{
    public IContainer Container { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;
    public string KeycloakUrl { get; private set; } = null!;

    public const string Realm = "test-realm";
    public const string ClientId = "test-client";
    public const string ClientSecret = "test-secret";
    public const string TestUsername = "testuser";
    public const string TestPassword = "testpass";

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("quay.io/keycloak/keycloak:26.0")
            .WithCommand("start-dev")
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithPortBinding(8080, true)
            // Строка «Listening on» в логе появляется раньше, чем admin-API начинает
            // отвечать, поэтому ждём готовность именно по HTTP, а не паузой в тесте.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Listening on: http://0.0.0.0:8080")
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPath("/realms/master/.well-known/openid-configuration")
                    .ForPort(8080)))
            .Build();

        await Container.StartAsync();

        var port = Container.GetMappedPublicPort(8080);
        KeycloakUrl = $"http://localhost:{port}";

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(KeycloakUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Setup realm, client, and user
        await SetupKeycloakAsync();
    }

    public async Task DisposeAsync()
    {
        HttpClient.Dispose();
        await Container.DisposeAsync();
    }

    private async Task SetupKeycloakAsync()
    {
        var adminToken = await GetAdminTokenAsync();
        await CreateRealmAsync(adminToken);
        await CreateClientAsync(adminToken);
        await CreateUserAsync(adminToken);
    }

    public async Task<string> GetAdminTokenAsync()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = "admin"
        });

        var response = await HttpClient.PostAsync("/realms/master/protocol/openid-connect/token", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);
        return tokenResponse!.AccessToken!;
    }

    private async Task CreateRealmAsync(string adminToken)
    {
        var realm = new
        {
            realm = Realm,
            enabled = true,
            sslRequired = "none"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/realms");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(realm),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await HttpClient.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create realm: {response.StatusCode} - {error}");
        }
    }

    private async Task CreateClientAsync(string adminToken)
    {
        var client = new
        {
            clientId = ClientId,
            enabled = true,
            protocol = "openid-connect",
            publicClient = false,
            secret = ClientSecret,
            directAccessGrantsEnabled = true,
            standardFlowEnabled = true,
            redirectUris = new[] { "*" },
            webOrigins = new[] { "*" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/realms/{Realm}/clients");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(client),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await HttpClient.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create client: {response.StatusCode} - {error}");
        }
    }

    private async Task CreateUserAsync(string adminToken)
    {
        // Create user with credentials in one request
        var user = new
        {
            username = TestUsername,
            email = "test@example.com",
            firstName = "Test",
            lastName = "User",
            emailVerified = true,
            enabled = true,
            requiredActions = Array.Empty<string>(),
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = TestPassword,
                    temporary = false
                }
            }
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"/admin/realms/{Realm}/users");
        createRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(user),
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await HttpClient.SendAsync(createRequest);
        if (createResponse.StatusCode != HttpStatusCode.Created && createResponse.StatusCode != HttpStatusCode.Conflict)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create user: {createResponse.StatusCode} - {error}");
        }
    }

    public class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
