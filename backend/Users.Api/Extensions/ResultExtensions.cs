using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Users.Entities.Errors;

namespace Users.Api.Extensions;

/// <summary>
/// Граница «результат бизнес-логики → HTTP-ответ» модулей users и authorization.
/// Логика возвращает нативный кортеж <c>(значение, ошибка)</c> либо просто <c>Error?</c>
/// (ADR-0004); превращение в <see cref="ActionResult"/> живёт здесь, в API-слое.
/// </summary>
public static class ResultExtensions
{
    public static async Task<IActionResult> AsActionResultAsync(
        this Task<Error?> operationTask,
        HttpContext context,
        int successStatusCode = 200)
    {
        var error = await operationTask;

        return error.AsActionResult(context, successStatusCode);
    }

    public static async Task<IActionResult> AsActionResultAsync<TValue>(
        this Task<(TValue? Value, Error? Error)> operationTask,
        HttpContext context,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(context, successStatusCode);
    }

    public static async Task<IActionResult> AsActionResultAsync<TValue, TView>(
        this Task<(TValue? Value, Error? Error)> operationTask,
        HttpContext context,
        Func<TValue, TView> toView,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(context, toView, successStatusCode);
    }

    /// <summary>
    /// То же, что AsActionResultAsync с маппером, но результат типизирован
    /// контрактным DTO: сгенерированные из OpenAPI базы объявляют
    /// ActionResult&lt;T&gt;, и расхождение с контрактом ловит компилятор.
    /// </summary>
    public static async Task<ActionResult<TContract>> AsContractResultAsync<TValue, TContract>(
        this Task<(TValue? Value, Error? Error)> operationTask,
        HttpContext context,
        Func<TValue, TContract> toContract,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(context, toContract, successStatusCode);
    }

    public static ActionResult AsActionResult(
        this Error? error,
        HttpContext context,
        int successStatusCode = 200)
    {
        if (error is null)
        {
            return new StatusCodeResult(successStatusCode);
        }

        return error.ToProblemDetails(context);
    }

    public static ActionResult AsActionResult<TValue>(
        this (TValue? Value, Error? Error) operation,
        HttpContext context,
        int successStatusCode = 200)
    {
        if (operation.Error is null)
        {
            return new JsonResult(operation.Value)
            {
                StatusCode = successStatusCode
            };
        }

        return operation.Error.ToProblemDetails(context);
    }

    public static ActionResult AsActionResult<TValue, TView>(
        this (TValue? Value, Error? Error) operation,
        HttpContext context,
        Func<TValue, TView> toView,
        int successStatusCode = 200)
    {
        if (operation.Error is not null)
        {
            return operation.Error.ToProblemDetails(context);
        }

        if (operation.Value == null)
        {
            return new JsonResult(ConvertNullToContract(typeof(TValue)))
            {
                StatusCode = successStatusCode
            };
        }

        return new JsonResult(toView(operation.Value))
        {
            StatusCode = successStatusCode
        };
    }

    private static readonly object EmptyObject = new { };

    private static object ConvertNullToContract(Type type)
    {
        var typeIsArray = typeof(ICollection).IsAssignableFrom(type);
        var typeIsDictionary = typeof(IDictionary).IsAssignableFrom(type);
        if (typeIsArray && !typeIsDictionary)
        {
            return Array.Empty<object>();
        }

        return EmptyObject;
    }
}
