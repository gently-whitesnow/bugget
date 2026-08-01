using Bugget.Api.Authorization.Abstractions;

namespace Bugget.Api.Authorization.Fake.Models;

/// <summary>
/// IExternalUser implementation for fake/development authentication.
/// </summary>
public sealed record FakeExternalUser(
    string ExternalId,
    string? Name,
    string? ImageUrl) : IExternalUser;
