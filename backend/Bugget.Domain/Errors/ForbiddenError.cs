namespace Bugget.Domain.Errors;

/// <summary>Прав недостаточно — 403.</summary>
public sealed record ForbiddenError(string Code, string Title) : Error(Code, Title);
