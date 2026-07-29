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
        string code,
        string title,
        int status,
        string? detail = null,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var isServerError = status >= StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Type = TypePrefix + code,
            Title = isServerError ? InternalTitle : title,
            Status = status,
            Detail = isServerError ? null : detail ?? title
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id;
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return AsProblemResult(problem, status);
    }

    public static ObjectResult CreateValidation(HttpContext context, ModelStateDictionary modelState)
    {
        var problem = new ValidationProblemDetails(modelState)
        {
            Type = TypePrefix + "model_state_validation_error",
            Title = "Ошибка валидации модели запроса",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Ошибка валидации модели запроса"
        };
        problem.Extensions["code"] = "model_state_validation_error";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        return AsProblemResult(problem, StatusCodes.Status400BadRequest);
    }

    public static Task WriteAsync(HttpContext context, string code, string title, int status)
    {
        var result = Create(code, title, status);
        ((ProblemDetails)result.Value!).Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(result.Value);
    }

    private static ObjectResult AsProblemResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
