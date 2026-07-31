using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Extensions;
using Users.Entities.Errors;
using Xunit;

namespace Users.UnitTests;

/// <summary>
/// Граница «результат бизнес-логики → HTTP-ответ» модуля users: кортеж
/// <c>(значение, ошибка)</c> превращается в ActionResult (ADR-0004).
///
/// Проверяем ровно то, что видит фронт: статус, тело и подстановку пустого значения.
/// Ветка ошибки отдаёт problem+json того же кода, что и сама ошибка, — полный каталог
/// «код ошибки → статус» проверяется отдельно, в ProblemDetailsFactoryTests.
/// </summary>
public sealed class ResultExtensionsTests
{
    private static readonly NotFoundError SomeError = new("team_not_found_error", "Команда не найдена");

    [Fact]
    public async Task Void_result_without_error_returns_success_status()
    {
        var result = await Task.FromResult<Error?>(null).AsActionResultAsync(new DefaultHttpContext(), 204);

        Assert.Equal(204, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Void_result_with_error_returns_problem_details()
    {
        var result = await Task.FromResult<Error?>(SomeError).AsActionResultAsync(new DefaultHttpContext());

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Valued_result_returns_the_value_as_json()
    {
        var payload = new { Id = 1 };

        var result = await Task.FromResult<(object? Value, Error? Error)>((payload, null))
            .AsActionResultAsync(new DefaultHttpContext(), 201);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(201, json.StatusCode);
        Assert.Same(payload, json.Value);
    }

    [Fact]
    public async Task Valued_result_with_error_returns_problem_details()
    {
        var result = await Task.FromResult<(object? Value, Error? Error)>((null, SomeError))
            .AsActionResultAsync(new DefaultHttpContext());

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Mapped_result_applies_the_mapper()
    {
        var result = await Task.FromResult<(string? Value, Error? Error)>(("team", null))
            .AsActionResultAsync(new DefaultHttpContext(), value => value.Length);

        Assert.Equal(4, Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public async Task Mapped_result_with_error_skips_the_mapper()
    {
        var mapperCalled = false;

        var result = await Task.FromResult<(string? Value, Error? Error)>((null, SomeError))
            .AsActionResultAsync(new DefaultHttpContext(), _ =>
            {
                mapperCalled = true;
                return 0;
            });

        AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.False(mapperCalled);
    }

    /// <summary>
    /// Успех без значения — не 204 и не null в теле: контракт обещает объект или массив,
    /// поэтому наружу уходит пустой объект, а для коллекции — пустой массив.
    /// </summary>
    [Fact]
    public async Task Missing_value_becomes_an_empty_contract_shape()
    {
        var single = await Task.FromResult<(string? Value, Error? Error)>((null, null))
            .AsActionResultAsync(new DefaultHttpContext(), value => value.Length);

        Assert.NotNull(Assert.IsType<JsonResult>(single).Value);

        var collection = await Task.FromResult<(string[]? Value, Error? Error)>((null, null))
            .AsActionResultAsync(new DefaultHttpContext(), value => value.Length);

        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            Assert.IsType<JsonResult>(collection).Value!).Cast<object>());
    }

    [Fact]
    public async Task Contract_result_is_typed_by_the_contract_dto()
    {
        var result = await Task.FromResult<(string? Value, Error? Error)>(("team", null))
            .AsContractResultAsync(new DefaultHttpContext(), value => value.ToUpperInvariant());

        Assert.Equal("TEAM", Assert.IsType<JsonResult>(result.Result).Value);
    }

    private static void AssertProblem(IActionResult result, int expectedStatus)
    {
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);

        Assert.Equal(expectedStatus, problem.Status);
    }
}
