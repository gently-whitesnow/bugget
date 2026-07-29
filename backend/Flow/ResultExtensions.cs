using System;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Extensions;

public static class ResultExtensions
{
    public static async Task<IActionResult> AsActionResultAsync(
    this Task<ResultStruct> operationTask,
    int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(successStatusCode);
    }

    public static async Task<IActionResult> AsActionResultAsync<TValue>(
        this Task<ResultStruct<TValue>> operationTask,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(successStatusCode);
    }

    public static async Task<IActionResult> AsActionResultAsync<TValue, TView>(
        this Task<ResultStruct<TValue>> operationTask,
        Func<TValue, TView> toView,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(toView, successStatusCode);
    }

    /// <summary>
    /// То же, что AsActionResultAsync с маппером, но результат типизирован
    /// контрактным DTO: сгенерированные из OpenAPI базы объявляют
    /// ActionResult&lt;T&gt;, и расхождение с контрактом ловит компилятор.
    /// </summary>
    public static async Task<ActionResult<TContract>> AsContractResultAsync<TValue, TContract>(
        this Task<ResultStruct<TValue>> operationTask,
        Func<TValue, TContract> toContract,
        int successStatusCode = 200)
    {
        var operation = await operationTask;

        return operation.AsActionResult(toContract, successStatusCode);
    }

    public static ActionResult AsActionResult(
        this ResultStruct operation,
        int successStatusCode = 200)
    {
        if (operation.IsSuccess)
        {
            return new StatusCodeResult(successStatusCode);
        }

        return operation.Error!.ToProblemDetails();
    }

    public static ActionResult AsActionResult<TValue>(
        this ResultStruct<TValue> operation,
        int successStatusCode = 200)
    {
        if (operation.IsSuccess)
        {
            return new JsonResult(operation.Value)
            {
                StatusCode = successStatusCode
            };
        }

        return operation.Error!.ToProblemDetails();
    }

    public static ActionResult AsActionResult<TValue, TView>(
        this ResultStruct<TValue> operation,
        Func<TValue, TView> toView,
        int successStatusCode = 200)
    {
        if (operation.HasError)
        {
            return operation.Error!.ToProblemDetails();
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
