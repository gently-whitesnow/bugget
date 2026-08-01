using Bugget.Api.Extensions;
using Bugget.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.UnitTests;

/// <summary>
/// Граница «результат бизнес-логики → HTTP-ответ». Бизнес-логика возвращает кортеж
/// <c>(значение, ошибка)</c> либо <c>Error?</c> (ADR-0004), и весь выбор между телом
/// ответа и problem+json делается здесь, поэтому проверяется он напрямую, а не только
/// через контроллеры: у синхронных перегрузок вызывающих в контроллерах может не быть
/// вовсе, а поведение фронт видит то же самое.
///
/// Отдельно закреплено превращение null в контракт: пустая коллекция уезжает наружу
/// массивом, пустой объект — объектом. Фронт на это опирается.
/// </summary>
public sealed class ResultAdapterTests
{
    [Fact]
    public void Value_without_error_becomes_body_with_success_status()
    {
        var result = (Value: "ok", Error: (Error?)null).AsActionResult(new DefaultHttpContext(), StatusCodes.Status201Created);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(StatusCodes.Status201Created, json.StatusCode);
        Assert.Equal("ok", json.Value);
    }

    [Fact]
    public void Error_wins_over_value_and_becomes_problem_details()
    {
        var operation = (Value: "ignored", Error: (Error?)new NotFoundError("report_not_found", "Репорт не найден"));

        var result = operation.AsActionResult(new DefaultHttpContext());

        AssertProblem(result, StatusCodes.Status404NotFound, "report_not_found");
    }

    [Fact]
    public void Value_is_projected_to_view_before_it_leaves()
    {
        var operation = (Value: (int?)7, Error: (Error?)null);

        var result = operation.AsActionResult(new DefaultHttpContext(), value => $"#{value}");

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(StatusCodes.Status200OK, json.StatusCode);
        Assert.Equal("#7", json.Value);
    }

    [Fact]
    public void Error_skips_the_projection()
    {
        var operation = (Value: (int?)7, Error: (Error?)new ConflictError("report_closed", "Репорт закрыт"));

        Func<int?, string> toView = _ => throw new InvalidOperationException("проекция не должна вызываться на ошибке");

        var result = operation.AsActionResult(new DefaultHttpContext(), toView);

        AssertProblem(result, StatusCodes.Status409Conflict, "report_closed");
    }

    [Fact]
    public void Missing_collection_becomes_empty_array()
    {
        var operation = (Value: (string[]?)null, Error: (Error?)null);

        var result = operation.AsActionResult(new DefaultHttpContext(), values => values);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(Array.Empty<object>(), json.Value);
    }

    [Fact]
    public void Missing_dictionary_becomes_empty_object()
    {
        var operation = (Value: (Dictionary<string, string>?)null, Error: (Error?)null);

        var result = operation.AsActionResult(new DefaultHttpContext(), values => values);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
        Assert.False(json.Value is System.Collections.IEnumerable);
    }

    [Fact]
    public void Missing_object_becomes_empty_object()
    {
        var operation = (Value: (string?)null, Error: (Error?)null);

        var result = operation.AsActionResult(new DefaultHttpContext(), value => value);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
        Assert.False(json.Value is string);
    }

    [Fact]
    public void Unknown_error_kind_fails_loudly_instead_of_guessing_a_status()
    {
        var error = new UnmappedError("unmapped", "Неизвестная ошибка");

        Assert.Throws<NotImplementedException>(() => error.ToDescriptor());
    }

    private static void AssertProblem(ActionResult result, int expectedStatus, string expectedCode)
    {
        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, problem.StatusCode);

        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal(expectedStatus, details.Status);
        Assert.Equal(expectedCode, details.Extensions["code"]);
    }

    /// <summary>Ошибка, которой нет в известной иерархии: статус для неё вывести не из чего.</summary>
    private sealed record UnmappedError(string Code, string Title) : Error(Code, Title);
}
