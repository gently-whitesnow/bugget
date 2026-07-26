namespace FakeAuth;

/// <summary>
/// Configuration for fake authentication (local development only).
/// </summary>
public sealed class FakeAuthOptions
{
    /// <summary>
    /// Path to redirect after successful authorization.
    /// Used when 'next' query parameter is not provided.
    /// Default: "/"
    /// </summary>
    public string DefaultRedirectPath { get; init; } = "/";
}
