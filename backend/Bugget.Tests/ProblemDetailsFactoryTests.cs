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
    [Fact]
    public void Catalog_and_ad_hoc_descriptors_keep_all_existing_http_statuses()
    {
        var expectedStatuses = new Dictionary<string, int>
        {
            ["invalid_period"] = 400,
            ["scopes_required"] = 400,
            ["scopes_limit_exceeded"] = 400,
            ["scope_key_required"] = 400,
            ["duplicate_scope_key"] = 400,
            ["last_login_method"] = 400,
            ["invalid_source_user_id"] = 400,
            ["same_source_user"] = 400,
            ["source_not_found"] = 404,
            ["source_owns_workspaces"] = 409,
            ["merge_failed"] = 400,
            ["invalid_mattermost_user_id"] = 400,
            ["avatar_too_large"] = 400,
            ["avatar_format_not_allowed"] = 400,
            ["model_state_validation_error"] = 400,
            ["bad_request"] = 400,
            ["unauthorized"] = 401,
            ["forbidden"] = 403,
            ["method_not_allowed"] = 405,
            ["unsupported_media_type"] = 415,
            ["internal_server_error"] = 500,
            ["not_found"] = 404
        };

        var descriptors = ReadCatalog(typeof(global::Bugget.ProblemDescriptors).Assembly, "Bugget.ProblemDescriptors")
            .Concat(ReadCatalog(typeof(Users.Api.ProblemDescriptors).Assembly, "Users.Api.ProblemDescriptors"))
            .Concat(ReadCatalog(typeof(CommonProblemDescriptors).Assembly, "Bugget.Http.CommonProblemDescriptors"))
            .Append(new ProblemDescriptor(
                Bugget.BO.Errors.BoErrors.NotFoundError.Error,
                Bugget.BO.Errors.BoErrors.NotFoundError.Reason,
                StatusCodes.Status404NotFound))
            .ToArray();

        Assert.Equal(
            expectedStatuses.Keys.OrderBy(code => code, StringComparer.Ordinal),
            descriptors.Select(descriptor => descriptor.Code).Distinct().OrderBy(code => code, StringComparer.Ordinal));

        foreach (var descriptor in descriptors)
        {
            Assert.Equal(expectedStatuses[descriptor.Code], descriptor.Status);
        }

        // Один код — один заголовок и один статус. Каталог общий, и разъехавшийся
        // заголовок под тем же `type` означал бы, что дескриптор всё-таки не один.
        foreach (var byCode in descriptors.GroupBy(descriptor => descriptor.Code))
        {
            Assert.Single(byCode.Select(descriptor => (descriptor.Title, descriptor.Status)).Distinct());
        }
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

    [Fact]
    public void Bugget_domain_error_catalog_keeps_all_existing_codes_and_http_statuses()
    {
        var expectedStatuses = ExpectedStatuses(
            (400, [
                "attachment_file_extension_not_found", "attachment_file_name_invalid_chars",
                "attachment_file_not_selected_or_empty", "attachment_file_too_large",
                "attachment_limit_exceeded", "attachment_target_required", "attachment_type_not_allowed",
                "attachment_type_not_supported", "bug_must_have_one_field", "bug_steps_order_size_mismatch",
                "creator_user_id_required", "idempotency_key_required", "organization_id_required",
                "since_id_required", "team_id_required", "team_setting_not_found",
                "user_setting_not_found", "workspace_id_required", "workspace_setting_invalid_values",
                "workspace_setting_not_found"
            ]),
            (404, [
                "attachment_not_found", "bug_not_found", "bug_step_not_found", "bug_steps_not_found",
                "comment_not_found", "not_found", "report_link_not_found", "report_not_found",
                "team_settings_section_not_found", "user_comment_not_found", "user_settings_section_not_found",
                "workspace_settings_section_not_found"
            ]),
            (409, ["report_closed"]),
            (500, ["internal_server_error"]));

        var errors = ReadErrorCatalog<Monade.Error>(typeof(Bugget.BO.Errors.BoErrors))
            .Append(Bugget.BO.Errors.BoErrors.AttachmentTypeNotSupported("image/test"))
            .ToArray();

        AssertErrorCatalog(
            errors,
            expectedStatuses,
            error => Bugget.Extensions.ErrorExtensions.ToProblemDetails(error, new DefaultHttpContext()));
    }

    [Fact]
    public void Users_and_authorization_domain_error_catalogs_keep_all_existing_codes_and_http_statuses()
    {
        var expectedStatuses = ExpectedStatuses(
            (400, [
                "feature_not_implemented", "paid_feature_not_implemented",
                "team_max_users_count_error", "user_already_in_team_error"
            ]),
            (401, [
                "expired_access_token", "expired_refresh_token", "invalid_access_token",
                "invalid_refresh_token", "invalid_token", "user_not_active"
            ]),
            (403, [
                "forbidden_error", "self_hosted_mode_error", "self_hosted_mode_required_error",
                "user_not_in_team_error"
            ]),
            (404, ["not_found_error", "team_not_found_error", "user_not_found"]),
            (500, ["internal_server_error"]));

        var errors = ReadErrorCatalog<Flow.Error>(typeof(Users.BO.BoErrors))
            .Concat(ReadErrorCatalog<Flow.Error>(typeof(Authorization.Api.BoErrors)))
            .ToArray();

        AssertErrorCatalog(
            errors,
            expectedStatuses,
            error => Flow.Extensions.ErrorExtensions.ToProblemDetails(error, new DefaultHttpContext()));
    }

    private static ProblemDetails GetProblem(ProblemDescriptor descriptor, string? detail = null) =>
        Assert.IsType<ProblemDetails>(ProblemDetailsFactory.Create(new DefaultHttpContext(), descriptor, detail).Value);

    private static IEnumerable<ProblemDescriptor> ReadCatalog(System.Reflection.Assembly assembly, string typeName)
    {
        var catalog = assembly.GetType(typeName);
        Assert.NotNull(catalog);
        return catalog
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ProblemDescriptor))
            .Select(field => Assert.IsType<ProblemDescriptor>(field.GetValue(null)));
    }

    private static IEnumerable<TError> ReadErrorCatalog<TError>(Type catalog) =>
        catalog
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => typeof(TError).IsAssignableFrom(field.FieldType))
            .Select(field => Assert.IsAssignableFrom<TError>(field.GetValue(null)));

    private static Dictionary<string, int> ExpectedStatuses(
        params (int Status, string[] Codes)[] groups) =>
        groups
            .SelectMany(group => group.Codes.Select(code => (Code: code, group.Status)))
            .ToDictionary(item => item.Code, item => item.Status, StringComparer.Ordinal);

    private static void AssertErrorCatalog<TError>(
        IReadOnlyCollection<TError> errors,
        IReadOnlyDictionary<string, int> expectedStatuses,
        Func<TError, ActionResult> convert)
    {
        var actual = errors
            .Select(error =>
            {
                var result = Assert.IsType<ObjectResult>(convert(error));
                return (Result: result, Problem: Assert.IsType<ProblemDetails>(result.Value));
            })
            .ToArray();

        Assert.Equal(
            expectedStatuses.Keys.OrderBy(code => code, StringComparer.Ordinal),
            actual.Select(item => item.Problem.Extensions["code"] as string)
                .OrderBy(code => code, StringComparer.Ordinal));

        foreach (var (result, problem) in actual)
        {
            var code = Assert.IsType<string>(problem.Extensions["code"]);
            Assert.Equal(expectedStatuses[code], result.StatusCode);
            Assert.Equal(expectedStatuses[code], problem.Status);
        }
    }
}
