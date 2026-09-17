namespace Bugget.Domain.Errors;

/// <summary>Объекта нет — 404.</summary>
public sealed record NotFoundError(string Code, string Title) : Error(Code, Title);
