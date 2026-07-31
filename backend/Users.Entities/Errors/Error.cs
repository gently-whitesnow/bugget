namespace Users.Entities.Errors;

/// <summary>
/// Ошибка модулей users и authorization. Возвращается нативным кортежем
/// <c>(значение, ошибка)</c>, обёртки-монады над ней нет (ADR-0004). HTTP-статус выводится
/// в API-слое, в <c>Users.Api.Extensions.ErrorExtensions</c>.
/// </summary>
public abstract record Error;

/// <summary>Запрос невалиден по бизнес-правилам — 400.</summary>
public sealed record BadRequestError(string Error, string Reason) : Error;

/// <summary>Объекта нет — 404.</summary>
public sealed record NotFoundError(string Error, string Reason) : Error;

/// <summary>Аутентификация не прошла — 401.</summary>
public sealed record UnauthorizedError(string Error, string Reason) : Error;

/// <summary>Прав недостаточно — 403.</summary>
public sealed record ForbiddenError(string Error, string Reason) : Error;

/// <summary>Отказ на нашей стороне — 500.</summary>
public sealed record InternalServerError(string Error, string Reason) : Error;
