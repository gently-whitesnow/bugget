namespace Bugget.Domain.Errors;

/// <summary>Состояние объекта не допускает операцию — 409.</summary>
public sealed record ConflictError(string Code, string Title) : Error(Code, Title);
