namespace Bugget.Entities.Errors;

/// <summary>
/// Каноническая прикладная ошибка backend. Возвращается кортежем <c>(значение, ошибка)</c>,
/// обёртки-монады над ней нет (ADR-0004). Транспорт про неё ничего не знает: HTTP-статус
/// выводится единым адаптером API-слоя в <c>Bugget.Http</c>.
/// </summary>
public abstract record Error(string Code, string Title);

/// <summary>Запрос невалиден по бизнес-правилам — 400.</summary>
public sealed record BadRequestError(string Code, string Title) : Error(Code, Title);

/// <summary>Объекта нет — 404.</summary>
public sealed record NotFoundError(string Code, string Title) : Error(Code, Title);

/// <summary>Состояние объекта не допускает операцию — 409.</summary>
public sealed record ConflictError(string Code, string Title) : Error(Code, Title);

/// <summary>Аутентификация не прошла — 401.</summary>
public sealed record UnauthorizedError(string Code, string Title) : Error(Code, Title);

/// <summary>Прав недостаточно — 403.</summary>
public sealed record ForbiddenError(string Code, string Title) : Error(Code, Title);

/// <summary>Отказ на нашей стороне — 500.</summary>
public sealed record InternalServerError(string Code, string Title) : Error(Code, Title);
