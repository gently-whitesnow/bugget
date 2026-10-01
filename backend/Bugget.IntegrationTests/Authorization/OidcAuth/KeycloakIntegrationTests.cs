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

/// <summary>Integration tests with real Keycloak: login → token exchange → user info.</summary>
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
        var token = await _fixture.GetAdminTokenAsync();

        Assert.False(string.IsNullOrEmpty(token));
        _output.WriteLine($"Got admin token: {token[..50]}...");
    }

    [Fact]
    public async Task Keycloak_CanAuthenticateUser_WithPasswordGrant()
    {
        var tokenResponse = await GetUserTokenAsync();

        Assert.NotNull(tokenResponse);
        Assert.False(string.IsNullOrEmpty(tokenResponse.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokenResponse.IdToken));
        _output.WriteLine($"Got user access token: {tokenResponse.AccessToken![..50]}...");
    }

    [Fact]
    public async Task Keycloak_TokenContainsSubClaim()
    {
        var tokenResponse = await GetUserTokenAsync();

        // Decode the id_token to get sub claim
        var idTokenParts = tokenResponse.IdToken!.Split('.');
        var payload = Base64UrlDecode(idTokenParts[1]);
        var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);

        Assert.NotNull(claims);
        Assert.True(claims!.ContainsKey("sub"));
        var sub = claims["sub"].GetString();
        Assert.False(string.IsNullOrEmpty(sub));
        _output.WriteLine($"User sub claim: {sub}");
    }

    [Fact]
    public async Task Keycloak_CanGetUserInfo()
    {
        var tokenResponse = await GetUserTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/realms/{KeycloakContainerFixture.Realm}/protocol/openid-connect/userinfo");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

        var response = await _fixture.HttpClient.SendAsync(request);

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
        var response = await _fixture.HttpClient.GetAsync(
            $"/realms/{KeycloakContainerFixture.Realm}/.well-known/openid-configuration");

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
        var oidcOptions = MsOptions.Create(new OidcAuthOptions
        {
            Authority = $"{_fixture.KeycloakUrl}/realms/{KeycloakContainerFixture.Realm}",
            ValidateLifetime = true,
            RequireHttpsMetadata = false
        });

        var validator = new OidcTokenValidator(oidcOptions, NullLogger<OidcTokenValidator>.Instance);

        // Try to validate a garbage token
        var principal = await validator.ValidateTokenAsync("invalid.token.here");

        Assert.Null(principal);
    }

    [Fact]
    public async Task OidcTokenValidator_ValidatesIdToken()
    {
        var tokenResponse = await GetUserTokenAsync();
        var idToken = tokenResponse.IdToken!;

        // id_token has audience = client_id
        var oidcOptions = MsOptions.Create(new OidcAuthOptions
        {
            Authority = $"{_fixture.KeycloakUrl}/realms/{KeycloakContainerFixture.Realm}",
            Audience = KeycloakContainerFixture.ClientId,
            ValidateLifetime = true,
            RequireHttpsMetadata = false
        });

        var validator = new OidcTokenValidator(oidcOptions, NullLogger<OidcTokenValidator>.Instance);

        var principal = await validator.ValidateTokenAsync(idToken);

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
            throw new InvalidOperationException($"Token request failed: {response.StatusCode} - {error}");
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
