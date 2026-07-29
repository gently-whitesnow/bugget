using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Flow;

/// <summary>Единый HTTP-adapter для ошибок доменных модулей.</summary>
public static class ProblemDetailsFactory
{
    private const string TypePrefix = "urn:bugget:error:";
    private const string InternalTitle = "Внутренняя ошибка сервера";

    public static ObjectResult Create(
        ProblemDescriptor descriptor,
        string? detail = null,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var isServerError = descriptor.Status >= StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Type = TypePrefix + descriptor.Code,
            Title = isServerError ? InternalTitle : descriptor.Title,
            Status = descriptor.Status,
            Detail = isServerError ? null : detail ?? descriptor.Title
        };

        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = GetTraceId(context: null);
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return AsProblemResult(problem, descriptor.Status);
    }

    public static ObjectResult CreateValidation(HttpContext context, ModelStateDictionary modelState)
    {
        var problem = new ValidationProblemDetails(modelState)
        {
            Type = TypePrefix + ProblemDescriptors.ModelStateValidation.Code,
            Title = ProblemDescriptors.ModelStateValidation.Title,
            Status = ProblemDescriptors.ModelStateValidation.Status,
            Detail = ProblemDescriptors.ModelStateValidation.Title
        };
        problem.Extensions["code"] = ProblemDescriptors.ModelStateValidation.Code;
        problem.Extensions["traceId"] = GetTraceId(context);

        return AsProblemResult(problem, ProblemDescriptors.ModelStateValidation.Status);
    }

    public static Task WriteAsync(HttpContext context, ProblemDescriptor descriptor)
    {
        var result = Create(descriptor);
        ((ProblemDetails)result.Value!).Extensions["traceId"] = GetTraceId(context);
        context.Response.StatusCode = descriptor.Status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(result.Value);
    }

    private static ObjectResult AsProblemResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private static string GetTraceId(HttpContext? context) =>
        Activity.Current?.Id ??
        (!string.IsNullOrWhiteSpace(context?.TraceIdentifier) ? context.TraceIdentifier : Guid.NewGuid().ToString("N"));
}

public sealed record ProblemDescriptor(string Code, string Title, int Status);

public static class ProblemDescriptors
{
    public static readonly ProblemDescriptor ModelStateValidation = new("model_state_validation_error", "Ошибка валидации модели запроса", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InternalServerError = new("internal_server_error", "Внутренняя ошибка сервера", StatusCodes.Status500InternalServerError);
    public static readonly ProblemDescriptor NotFound = new("not_found", "Объект не найден", StatusCodes.Status404NotFound);
    public static readonly ProblemDescriptor InvalidPeriod = new("invalid_period", "Некорректный период", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopesRequired = new("scopes_required", "Не переданы области подсчёта", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopesLimitExceeded = new("scopes_limit_exceeded", "Превышен лимит областей подсчёта", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor ScopeKeyRequired = new("scope_key_required", "Не передан ключ области", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor DuplicateScopeKey = new("duplicate_scope_key", "Ключ области повторяется", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor LastLoginMethod = new("last_login_method", "Нельзя отвязать единственный способ входа", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InvalidSourceUserId = new("invalid_source_user_id", "Некорректный sourceUserId", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor SameSourceUser = new("same_source_user", "Нельзя объединить аккаунт сам с собой", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor SourceNotFound = new("source_not_found", "Исходный аккаунт не найден", StatusCodes.Status404NotFound);
    public static readonly ProblemDescriptor SourceOwnsWorkspaces = new("source_owns_workspaces", "Исходный аккаунт владеет рабочими пространствами", StatusCodes.Status409Conflict);
    public static readonly ProblemDescriptor MergeFailed = new("merge_failed", "Не удалось объединить аккаунты", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InvalidMattermostUserId = new("invalid_mattermost_user_id", "Некорректный Mattermost User ID", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor AvatarTooLarge = new("avatar_too_large", "Размер файла не должен превышать 200 КБ", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor AvatarFormatNotAllowed = new("avatar_format_not_allowed", "Недопустимый формат файла. Разрешены: JPEG, PNG, GIF, WebP", StatusCodes.Status400BadRequest);
}
