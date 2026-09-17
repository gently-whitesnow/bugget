using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Http;

/// <summary>
/// Граница <c>long ↔ wire</c> для схемы <c>Int64String</c> (<c>specs/contracts/shared.yaml</c>).
/// Строкой, потому что JSON-число у клиента — double: всё больше 2^53−1 молча теряет точность.
/// </summary>
public static class WireInt64
{
    private const string RouteValueError =
        "Ожидается неотрицательное 64-битное целое строкой: `0` либо `[1-9][0-9]*` " +
        "без знака, ведущих нулей и разделителей, в диапазоне 0..9223372036854775807.";

    // Культура инвариантная явно: у культуры потока свои цифры и знак, а канон один.
    public static string ToWire(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // long.TryParse с NumberStyles.None пропускает 007 и не-ASCII цифры, поэтому форма проверяется до него.
    public static bool TryParse(string? wire, out long value)
    {
        value = 0;

        if (string.IsNullOrEmpty(wire))
        {
            return false;
        }

        if (wire.Length > 1 && wire[0] == '0')
        {
            return false;
        }

        foreach (var symbol in wire)
        {
            if (symbol is < '0' or > '9')
            {
                return false;
            }
        }

        return long.TryParse(wire, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary><c>null</c>, если сегмент каноничен; иначе готовый <c>400 model_state_validation_error</c>, как при отказе связывания модели.</summary>
    public static ActionResult? TryBindRouteValue(
        HttpContext context,
        string parameterName,
        string? wire,
        out long value)
    {
        if (TryParse(wire, out value))
        {
            return null;
        }

        var modelState = new ModelStateDictionary();
        modelState.AddModelError(parameterName, RouteValueError);
        return ProblemDetailsFactory.CreateValidation(context, modelState);
    }
}
