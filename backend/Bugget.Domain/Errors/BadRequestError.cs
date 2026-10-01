namespace Bugget.Domain.Errors;

/// <summary>Запрос невалиден по бизнес-правилам — 400.</summary>
public sealed record BadRequestError(string Code, string Title) : Error(Code, Title);
