namespace Bugget.Entities.Errors;

/// <summary>
/// Доменная ошибка модуля bugget. Возвращается кортежем <c>(значение, ошибка)</c>,
/// обёртки-монады над ней нет (ADR-0004). Транспорт про неё ничего не знает: HTTP-статус
/// выводится в API-слое, в <c>Bugget.Extensions.ErrorExtensions</c>.
/// </summary>
public abstract record Error;

/// <summary>Запрос невалиден по бизнес-правилам — 400.</summary>
public sealed record BadRequestError(string Error, string Reason) : Error;

/// <summary>Объекта нет — 404.</summary>
public sealed record NotFoundError(string Error, string Reason) : Error;

/// <summary>Состояние объекта не допускает операцию — 409.</summary>
public sealed record ConflictError(string Error, string Reason) : Error;

/// <summary>Отказ на нашей стороне — 500.</summary>
public sealed record InternalServerError(string Error, string Reason) : Error;
