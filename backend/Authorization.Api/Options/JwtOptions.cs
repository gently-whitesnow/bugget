using System;

namespace Authorization.Options;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public TimeSpan AccessLifetime { get; init; }
    public TimeSpan RefreshLifetime { get; init; }
}
