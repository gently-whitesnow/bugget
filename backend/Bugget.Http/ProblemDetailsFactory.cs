using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Bugget.Http;

public sealed record ProblemDescriptor(string Code, string Title, int Status);

public static class CommonProblemDescriptors
{
    public static readonly ProblemDescriptor ModelStateValidation = new("model_state_validation_error", "Ошибка валидации модели запроса", StatusCodes.Status400BadRequest);
    public static readonly ProblemDescriptor InternalServerError = new("internal_server_error", "Внутренняя ошибка сервера", StatusCodes.Status500InternalServerError);
}

public static class ProblemDetailsFactory
{
    private const string TypePrefix = "urn:bugget:error:";
    private const string InternalTitle = "Внутренняя ошибка сервера";
    private const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// Имена, которые прикладной словарь extensions занять не может: RFC-поля и вычисляемые
    /// фабрикой <c>code</c>/<c>traceId</c>. Инвариант «type и code выводятся из одного
    /// дескриптора» иначе разваливается снаружи — достаточно передать свой <c>code</c>.
    /// Конфликт не 500-ит запрос: прикладное значение молча не попадает в ответ, канонические
    /// поля всегда выигрывают. Сравнение регистронезависимое: под snake_case-политикой
    /// <c>Code</c> уехал бы отдельным ключом рядом с каноническим.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "type", "title", "status", "detail", "instance", "code", "traceId"
    };

    public static ObjectResult Create(HttpContext context, ProblemDescriptor descriptor, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var isServerError = descriptor.Status >= StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Type = TypePrefix + descriptor.Code,
            Title = isServerError ? InternalTitle : descriptor.Title,
            Status = descriptor.Status,
            Detail = isServerError ? null : detail
        };
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                if (ReservedNames.Contains(key))
                {
                    continue;
                }

                problem.Extensions[key] = value;
            }
        }

        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = GetTraceId(context);

        return AsResult(problem, descriptor.Status);
    }

    public static ObjectResult CreateValidation(ActionContext context)
    {
        var descriptor = CommonProblemDescriptors.ModelStateValidation;
        // Ключи уже в wire-форме: за это отвечает SystemTextJsonValidationMetadataProvider,
        // зарегистрированный в MVC-пайплайне. Он знает JSON-имя каждого свойства на любой
        // глубине, поэтому вложенный путь `scopes[0].key` нормализуется целиком — своей
        // таблицы имён здесь нет и быть не должно.
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = TypePrefix + descriptor.Code,
            Title = descriptor.Title,
            Status = descriptor.Status
        };
        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = GetTraceId(context.HttpContext);
        return AsResult(problem, descriptor.Status);
    }

    public static ObjectResult CreateValidation(HttpContext context, ModelStateDictionary modelState) =>
        CreateValidation(new ActionContext(context, new RouteData(), new ActionDescriptor(), modelState));

    public static Task WriteAsync(HttpContext context, ProblemDescriptor descriptor)
    {
        var result = Create(context, descriptor);
        context.Response.StatusCode = descriptor.Status;
        // Content-type задаётся аргументом, а не свойством Response: WriteAsJsonAsync
        // перетирает ранее выставленное значение своим application/json, и ответ middleware
        // переставал быть problem+json.
        return context.Response.WriteAsJsonAsync(result.Value, GetJsonOptions(context), ProblemContentType);
    }

    private static ObjectResult AsResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add(ProblemContentType);
        return result;
    }

    /// <summary>
    /// <c>traceId</c> — обязательное поле ответа, а оба обычных источника могут быть пустыми:
    /// <see cref="Activity.Current"/> отсутствует без включённой трассировки, а
    /// <see cref="HttpContext.TraceIdentifier"/> — пустая строка, если его обнулили выше по
    /// пайплайну. Приоритет источников прежний; сгенерированный fallback записывается обратно в
    /// контекст, чтобы логи и ответ ссылались на один и тот же идентификатор.
    /// </summary>
    private static string GetTraceId(HttpContext context)
    {
        var activityId = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(activityId))
        {
            return activityId;
        }

        if (!string.IsNullOrEmpty(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        var generated = Guid.NewGuid().ToString("n");
        context.TraceIdentifier = generated;
        return generated;
    }

    private static JsonSerializerOptions GetJsonOptions(HttpContext context) =>
        (context.Features.Get<IServiceProvidersFeature>()?.RequestServices
            .GetService(typeof(IOptions<JsonOptions>)) as IOptions<JsonOptions>)?.Value.JsonSerializerOptions
        ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
}
