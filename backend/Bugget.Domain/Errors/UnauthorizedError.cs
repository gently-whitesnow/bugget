namespace Bugget.Domain.Errors;

/// <summary>Аутентификация не прошла — 401.</summary>
public sealed record UnauthorizedError(string Code, string Title) : Error(Code, Title);
