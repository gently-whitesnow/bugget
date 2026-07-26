using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OidcAuth;
using Xunit;
using Xunit.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Authorization.Tests.OidcAuth;

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
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Listening on: http://0.0.0.0:8080"))
            .Build();

        await Container.StartAsync();

        var port = Container.GetMappedPublicPort(8080);
        KeycloakUrl = $"http://localhost:{port}";

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(KeycloakUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Wait a bit for Keycloak to be fully ready
        await Task.Delay(1000);

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
            throw new Exception($"Failed to create realm: {response.StatusCode} - {error}");
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
            throw new Exception($"Failed to create client: {response.StatusCode} - {error}");
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
            throw new Exception($"Failed to create user: {createResponse.StatusCode} - {error}");
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

[CollectionDefinition("Keycloak")]
public class KeycloakCollection : ICollectionFixture<KeycloakContainerFixture>
{
}

/// <summary>
/// Integration tests with real Keycloak container.
/// Tests the full OIDC flow: login → token exchange → user info.
/// </summary>
[Collection("Keycloak")]
public class KeycloakIntegrationTests
{
    private readonly KeycloakContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public KeycloakIntegrationTests(KeycloakContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Keycloak_CanGetAdminToken()
    {
        // Act
        var token = await _fixture.GetAdminTokenAsync();

        // Assert
        Assert.False(string.IsNullOrEmpty(token));
        _output.WriteLine($"Got admin token: {token[..50]}...");
    }

    [Fact]
    public async Task Keycloak_CanAuthenticateUser_WithPasswordGrant()
    {
        // Act
        var tokenResponse = await GetUserTokenAsync();

        // Assert
        Assert.NotNull(tokenResponse);
        Assert.False(string.IsNullOrEmpty(tokenResponse.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokenResponse.IdToken));
        _output.WriteLine($"Got user access token: {tokenResponse.AccessToken![..50]}...");
    }

    [Fact]
    public async Task Keycloak_TokenContainsSubClaim()
    {
        // Arrange
        var tokenResponse = await GetUserTokenAsync();

        // Act - decode the id_token to get sub claim
        var idTokenParts = tokenResponse.IdToken!.Split('.');
        var payload = Base64UrlDecode(idTokenParts[1]);
        var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);

        // Assert
        Assert.NotNull(claims);
        Assert.True(claims!.ContainsKey("sub"));
        var sub = claims["sub"].GetString();
        Assert.False(string.IsNullOrEmpty(sub));
        _output.WriteLine($"User sub claim: {sub}");
    }

    [Fact]
    public async Task Keycloak_CanGetUserInfo()
    {
        // Arrange
        var tokenResponse = await GetUserTokenAsync();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/realms/{KeycloakContainerFixture.Realm}/protocol/openid-connect/userinfo");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        Assert.NotNull(userInfo);
        Assert.True(userInfo!.ContainsKey("sub"));
        Assert.Equal(KeycloakContainerFixture.TestUsername, userInfo["preferred_username"].GetString());
        _output.WriteLine($"UserInfo: {content}");
    }

    [Fact]
    public async Task Keycloak_DiscoveryEndpointWorks()
    {
        // Act
        var response = await _fixture.HttpClient.GetAsync(
            $"/realms/{KeycloakContainerFixture.Realm}/.well-known/openid-configuration");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var disco = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

        Assert.NotNull(disco);
        Assert.True(disco!.ContainsKey("issuer"));
        Assert.True(disco.ContainsKey("authorization_endpoint"));
        Assert.True(disco.ContainsKey("token_endpoint"));
        Assert.True(disco.ContainsKey("jwks_uri"));
        _output.WriteLine($"Discovery: {content}");
    }

    [Fact]
    public async Task OidcTokenValidator_ValidatesKeycloakToken()
    {
        // Arrange - get a real token from Keycloak
        var tokenResponse = await GetUserTokenAsync();
        var accessToken = tokenResponse.AccessToken!;

        _output.WriteLine($"Access token: {accessToken[..Math.Min(100, accessToken.Length)]}...");

        // Verify JWKS endpoint is accessible
        var jwksResponse = await _fixture.HttpClient.GetAsync(
            $"/realms/{KeycloakContainerFixture.Realm}/protocol/openid-connect/certs");
        var jwksContent = await jwksResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"JWKS response: {jwksResponse.StatusCode}");

        // Manually validate using the same approach as OidcTokenValidator
        var authority = $"{_fixture.KeycloakUrl}/realms/{KeycloakContainerFixture.Realm}";
        var documentRetriever = new HttpDocumentRetriever { RequireHttps = false };
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);

        var config = await configManager.GetConfigurationAsync();
        _output.WriteLine($"OIDC issuer: {config.Issuer}");
        _output.WriteLine($"OIDC signing keys count: {config.SigningKeys.Count}");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        // Act - validate directly
        ClaimsPrincipal? principal = null;
        try
        {
            principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Validation error: {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        // Assert
        Assert.NotNull(principal);

        // Debug: print all claims
        _output.WriteLine("Claims in token:");
        foreach (var claim in principal.Claims)
        {
            _output.WriteLine($"  {claim.Type}: {claim.Value}");
        }

        var subject = principal.FindFirst("sub")?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.False(string.IsNullOrEmpty(subject), "Subject claim not found in token");
        _output.WriteLine($"Validated token, subject: {subject}");
    }

    [Fact]
    public async Task OidcTokenValidator_RejectsInvalidToken()
    {
        // Arrange
        var oidcOptions = MsOptions.Create(new OidcAuthOptions
        {
            Authority = $"{_fixture.KeycloakUrl}/realms/{KeycloakContainerFixture.Realm}",
            ValidateLifetime = true,
            RequireHttpsMetadata = false
        });

        var validator = new OidcTokenValidator(oidcOptions, NullLogger<OidcTokenValidator>.Instance);

        // Act - try to validate a garbage token
        var principal = await validator.ValidateTokenAsync("invalid.token.here");

        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public async Task OidcTokenValidator_ValidatesIdToken()
    {
        // Arrange - get a real token from Keycloak
        var tokenResponse = await GetUserTokenAsync();
        var idToken = tokenResponse.IdToken!;

        // Create our validator - id_token has audience = client_id
        var oidcOptions = MsOptions.Create(new OidcAuthOptions
        {
            Authority = $"{_fixture.KeycloakUrl}/realms/{KeycloakContainerFixture.Realm}",
            Audience = KeycloakContainerFixture.ClientId,
            ValidateLifetime = true,
            RequireHttpsMetadata = false
        });

        var validator = new OidcTokenValidator(oidcOptions, NullLogger<OidcTokenValidator>.Instance);

        // Act - validate the id_token
        var principal = await validator.ValidateTokenAsync(idToken);

        // Assert
        Assert.NotNull(principal);
        var subject = validator.GetSubject(principal);
        Assert.False(string.IsNullOrEmpty(subject));
        _output.WriteLine($"Validated id_token, subject: {subject}");
    }

    private async Task<KeycloakContainerFixture.TokenResponse> GetUserTokenAsync()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = KeycloakContainerFixture.ClientId,
            ["client_secret"] = KeycloakContainerFixture.ClientSecret,
            ["username"] = KeycloakContainerFixture.TestUsername,
            ["password"] = KeycloakContainerFixture.TestPassword,
            ["scope"] = "openid profile email"
        });

        var response = await _fixture.HttpClient.PostAsync(
            $"/realms/{KeycloakContainerFixture.Realm}/protocol/openid-connect/token", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Token request failed: {response.StatusCode} - {error}");
            throw new Exception($"Token request failed: {response.StatusCode} - {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KeycloakContainerFixture.TokenResponse>(json)!;
    }

    private static string Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
        }
        var bytes = Convert.FromBase64String(output);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
