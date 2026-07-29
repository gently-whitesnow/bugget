using System.Diagnostics;
using System.Text.Json;
using Bugget.Extensions;
using Bugget.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Monade.Errors;

namespace Bugget.Tests;

public sealed class ProblemDetailsFactoryTests
{
    [Theory]
    [InlineData("invalid_period", 400)]
    [InlineData("not_found", 404)]
    [InlineData("source_owns_workspaces", 409)]
    [InlineData("internal_server_error", 500)]
    public void Descriptor_keeps_existing_http_status(string code, int expectedStatus)
    {
        var descriptor = code switch
        {
            "invalid_period" => global::Bugget.ProblemDescriptors.InvalidPeriod,
            "not_found" => new ProblemDescriptor("not_found", "Объект не найден", 404),
            "source_owns_workspaces" => new ProblemDescriptor("source_owns_workspaces", "Исходный аккаунт владеет рабочими пространствами", 409),
            _ => CommonProblemDescriptors.InternalServerError
        };

        Assert.Equal(expectedStatus, descriptor.Status);
    }

    [Fact]
    public void Type_and_code_are_derived_from_the_same_descriptor_code()
    {
        var problem = GetProblem(global::Bugget.ProblemDescriptors.DuplicateScopeKey);

        Assert.Equal("urn:bugget:error:" + problem.Extensions["code"], problem.Type);
    }

    /// <summary>
    /// Прикладной словарь extensions не может занять зарезервированные имена: иначе вызывающий
    /// разводит `type` и `code` одним параметром, а корреляционный id подменяется чужим.
    /// </summary>
    [Fact]
    public void Reserved_extensions_cannot_be_overwritten_by_caller()
    {
        var descriptor = global::Bugget.ProblemDescriptors.DuplicateScopeKey;
        var context = new DefaultHttpContext();
        var expectedTraceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = Assert.IsType<ProblemDetails>(ProblemDetailsFactory.Create(context, descriptor, extensions: new Dictionary<string, object?>
        {
            ["code"] = "other_code",
            ["traceId"] = "подменённый",
            ["type"] = "urn:bugget:error:other_code",
            ["title"] = "чужой заголовок",
            ["status"] = 418,
            ["detail"] = "чужая деталь",
            ["instance"] = "/чужой/путь",
            ["key"] = "прикладное поле проходит"
        }).Value);

        Assert.Equal(descriptor.Code, problem.Extensions["code"]);
        Assert.Equal("urn:bugget:error:" + problem.Extensions["code"], problem.Type);
        Assert.Equal(expectedTraceId, problem.Extensions["traceId"]);
        Assert.Equal(descriptor.Title, problem.Title);
        Assert.Equal(descriptor.Status, problem.Status);
        Assert.Null(problem.Instance);
        Assert.Equal("прикладное поле проходит", problem.Extensions["key"]);
    }

    /// <summary>
    /// Регистр не спасает: под snake_case-политикой ключ `Code` уехал бы на провод отдельным
    /// полем рядом с каноническим, и у ответа стало бы два кода.
    /// </summary>
    [Fact]
    public void Reserved_extensions_are_rejected_case_insensitively()
    {
        var descriptor = global::Bugget.ProblemDescriptors.DuplicateScopeKey;

        var problem = Assert.IsType<ProblemDetails>(ProblemDetailsFactory.Create(new DefaultHttpContext(), descriptor, extensions: new Dictionary<string, object?>
        {
            ["Code"] = "other_code",
            ["TRACEID"] = "подменённый"
        }).Value);

        Assert.False(problem.Extensions.ContainsKey("Code"));
        Assert.False(problem.Extensions.ContainsKey("TRACEID"));
        Assert.Equal(descriptor.Code, problem.Extensions["code"]);
    }

    [Fact]
    public void Rfc_fields_and_extensions_preserve_their_wire_names_under_snake_case_policy()
    {
        var json = JsonSerializer.Serialize(GetProblem(global::Bugget.ProblemDescriptors.InvalidPeriod, "Некорректный период"), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("\"type\"", json);
        Assert.Contains("\"title\"", json);
        Assert.Contains("\"status\"", json);
        Assert.Contains("\"detail\"", json);
        Assert.Contains("\"code\"", json);
        Assert.Contains("\"traceId\"", json);
        Assert.DoesNotContain("trace_id", json);
    }

    [Fact]
    public void Validation_problem_preserves_error_keys_and_values()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("report_title", "Поле обязательно");
        var context = new DefaultHttpContext();

        var problem = Assert.IsType<ValidationProblemDetails>(ProblemDetailsFactory.CreateValidation(context, modelState).Value);

        Assert.Equal("model_state_validation_error", problem.Extensions["code"]);
        Assert.Equal(["Поле обязательно"], problem.Errors["report_title"]);
    }

    [Fact]
    public void Validation_problem_serializes_exact_error_key_and_value()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("report_title", "Поле обязательно");
        var problem = Assert.IsType<ValidationProblemDetails>(
            ProblemDetailsFactory.CreateValidation(new DefaultHttpContext(), modelState).Value);

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        using var document = JsonDocument.Parse(json);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Equal("Поле обязательно", errors.GetProperty("report_title")[0].GetString());
    }

    [Fact]
    public void Server_error_redacts_detail_and_has_trace_fallback()
    {
        var problem = GetProblem(CommonProblemDescriptors.InternalServerError, "секрет исключения");

        Assert.Equal("Внутренняя ошибка сервера", problem.Title);
        Assert.Null(problem.Detail);
        Assert.False(string.IsNullOrWhiteSpace(problem.Extensions["traceId"] as string));
    }

    [Fact]
    public async Task Server_error_writer_redacts_payload_and_generates_trace_fallback()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await ProblemDetailsFactory.WriteAsync(context, CommonProblemDescriptors.InternalServerError);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("Внутренняя ошибка сервера", document.RootElement.GetProperty("title").GetString());
        Assert.False(document.RootElement.TryGetProperty("detail", out _));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    /// <summary>
    /// В настоящем пайплайне запрос идёт под Activity, и корреляция строится по её id, а не по
    /// TraceIdentifier: fallback — это ветка для голого контекста, а не основной путь.
    /// </summary>
    [Fact]
    public void Trace_id_comes_from_the_current_activity_when_there_is_one()
    {
        using var activity = new Activity("problem-details-test").Start();

        var problem = GetProblem(CommonProblemDescriptors.InternalServerError);

        Assert.Equal(activity.Id, problem.Extensions["traceId"]);
        Assert.NotEqual(new DefaultHttpContext().TraceIdentifier, problem.Extensions["traceId"]);
    }

    /// <summary>
    /// Writer обязан уважать настройки сериализации приложения, когда контейнер запроса
    /// доступен: иначе middleware отдавало бы форму, отличную от той, что уходит из контроллеров.
    /// </summary>
    [Fact]
    public async Task Writer_uses_application_json_options_when_request_services_are_available()
    {
        var services = new ServiceCollection();
        services.AddOptions<JsonOptions>().Configure(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        await using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();

        await ProblemDetailsFactory.WriteAsync(context, CommonProblemDescriptors.InternalServerError);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("internal_server_error", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.False(document.RootElement.TryGetProperty("trace_id", out _));
    }

    [Theory]
    [InlineData("bad_request", "invalid_period", 400)]
    [InlineData("not_found", "not_found", 404)]
    [InlineData("conflict", "duplicate_scope_key", 409)]
    [InlineData("internal", "internal_server_error", 500)]
    public void Existing_domain_errors_keep_their_http_status(string kind, string expectedCode, int expectedStatus)
    {
        Monade.Error error = kind switch
        {
            "bad_request" => new BadRequestError("invalid_period", "Некорректный период"),
            "not_found" => new NotFoundError("not_found", "Не найдено"),
            "conflict" => new ConflictError("duplicate_scope_key", "Повторяющийся ключ scope"),
            _ => new InternalServerError("internal_server_error", "Секретная причина")
        };

        var result = Assert.IsType<ObjectResult>(error.ToProblemDetails(new DefaultHttpContext()));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, problem.Extensions["code"]);
    }

    private static ProblemDetails GetProblem(ProblemDescriptor descriptor, string? detail = null) =>
        Assert.IsType<ProblemDetails>(ProblemDetailsFactory.Create(new DefaultHttpContext(), descriptor, detail).Value);
}
