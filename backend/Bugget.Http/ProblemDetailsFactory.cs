using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    public static ObjectResult Create(ProblemDescriptor descriptor, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null) =>
        Create(new DefaultHttpContext(), descriptor, detail, extensions);

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
        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = GetTraceId(context);
        if (extensions is not null)
            foreach (var (key, value) in extensions)
                problem.Extensions[key] = value;

        return AsResult(problem, descriptor.Status);
    }

    public static ObjectResult CreateValidation(HttpContext context, ModelStateDictionary modelState)
    {
        var descriptor = CommonProblemDescriptors.ModelStateValidation;
        var problem = new ValidationProblemDetails(modelState)
        {
            Type = TypePrefix + descriptor.Code,
            Title = descriptor.Title,
            Status = descriptor.Status
        };
        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = GetTraceId(context);
        return AsResult(problem, descriptor.Status);
    }

    public static Task WriteAsync(HttpContext context, ProblemDescriptor descriptor)
    {
        var result = Create(context, descriptor);
        context.Response.StatusCode = descriptor.Status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(result.Value);
    }

    private static ObjectResult AsResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private static string GetTraceId(HttpContext context) =>
        Activity.Current?.Id ?? (!string.IsNullOrWhiteSpace(context.TraceIdentifier) ? context.TraceIdentifier : Guid.NewGuid().ToString("N"));
}
