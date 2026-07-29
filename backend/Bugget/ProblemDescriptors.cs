using Bugget.Http;
using Microsoft.AspNetCore.Http;

namespace Bugget;

public static class ProblemDescriptors
{
    public static readonly ProblemDescriptor InvalidPeriod = new("invalid_period", "Некорректный период", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopesRequired = new("scopes_required", "Не переданы области подсчёта", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopesLimitExceeded = new("scopes_limit_exceeded", "Превышен лимит областей подсчёта", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopeKeyRequired = new("scope_key_required", "Не передан ключ области", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor DuplicateScopeKey = new("duplicate_scope_key", "Ключ области повторяется", StatusCodes.Status400BadRequest);
}
