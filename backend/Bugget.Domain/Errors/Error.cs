namespace Bugget.Domain.Errors;

/// <summary>
/// Каноническая прикладная ошибка backend. Возвращается кортежем <c>(значение, ошибка)</c>,
/// обёртки-монады над ней нет (ADR-0004). Транспорт про неё ничего не знает: HTTP-статус
/// выводится единым адаптером API-слоя в <c>Bugget.Api.Http</c>.
/// </summary>
public abstract record Error(string Code, string Title);
