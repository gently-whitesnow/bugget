namespace Bugget.Domain.Errors;

/// <summary>Отказ на нашей стороне — 500.</summary>
public sealed record InternalServerError(string Code, string Title) : Error(Code, Title);
