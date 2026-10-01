using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Http;

public static class ProblemDetailsFactory
{
    private const string TypePrefix = "urn:bugget:error:";
    private const string InternalTitle = "Внутренняя ошибка сервера";
    private const string ProblemContentType = "application/problem+json";

    // Имена, которые прикладные extensions занять не могут: RFC-поля и вычисляемые code/traceId, иначе инвариант
    // «type и code из одного дескриптора» ломается снаружи. Конфликт не 500-ит: прикладное значение молча отбрасывается.
    // Сравнение регистронезависимое: под snake_case-политикой Code уехал бы отдельным ключом рядом с каноническим.
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
        // Ключи уже в wire-форме: SystemTextJsonValidationMetadataProvider в MVC-пайплайне знает JSON-имя свойства
        // на любой глубине (`scopes[0].key`), поэтому своей таблицы имён здесь нет и быть не должно.
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
        // Content-type — аргументом, а не свойством Response: WriteAsJsonAsync перетирает его своим application/json.
        return context.Response.WriteAsJsonAsync(result.Value, GetJsonOptions(context), ProblemContentType);
    }

    private static ObjectResult AsResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add(ProblemContentType);
        return result;
    }

    // traceId обязателен, а Activity.Current и TraceIdentifier могут быть пустыми; сгенерированный fallback
    // пишется обратно в контекст, чтобы логи и ответ ссылались на один идентификатор.
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
