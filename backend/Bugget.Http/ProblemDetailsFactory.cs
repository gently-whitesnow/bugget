using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return AsResult(problem, descriptor.Status);
    }

    public static ObjectResult CreateValidation(ActionContext context)
    {
        var descriptor = CommonProblemDescriptors.ModelStateValidation;
        var problem = new ValidationProblemDetails(NormalizeBodyKeys(context))
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
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(result.Value, GetJsonOptions(context));
    }

    private static ObjectResult AsResult(ProblemDetails problem, int status)
    {
        var result = new ObjectResult(problem) { StatusCode = status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private static string GetTraceId(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;

    private static JsonSerializerOptions GetJsonOptions(HttpContext context) =>
        (context.Features.Get<IServiceProvidersFeature>()?.RequestServices
            .GetService(typeof(IOptions<JsonOptions>)) as IOptions<JsonOptions>)?.Value.JsonSerializerOptions
        ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private static ModelStateDictionary NormalizeBodyKeys(ActionContext context)
    {
        var bodyType = context.ActionDescriptor.Parameters
            .SingleOrDefault(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body)?.ParameterType;
        if (bodyType is null)
        {
            return context.ModelState;
        }

        var namingPolicy = (context.HttpContext.RequestServices
            .GetService(typeof(IOptions<JsonOptions>)) as IOptions<JsonOptions>)?.Value.JsonSerializerOptions.PropertyNamingPolicy;
        var wireNames = bodyType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => (Property: property.Name, WireName: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? namingPolicy?.ConvertName(property.Name) ?? property.Name))
            .ToDictionary(item => item.Property, item => item.WireName, StringComparer.OrdinalIgnoreCase);
        var normalized = new ModelStateDictionary();

        foreach (var (key, value) in context.ModelState)
        {
            var normalizedKey = wireNames.TryGetValue(key, out var wireName) ? wireName : key;
            normalized.SetModelValue(normalizedKey, value.RawValue, value.AttemptedValue);
            foreach (var error in value.Errors)
            {
                normalized.AddModelError(normalizedKey, error.ErrorMessage);
            }
        }

        return normalized;
    }
}
