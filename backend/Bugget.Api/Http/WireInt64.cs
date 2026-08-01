using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Http;

/// <summary>
/// Граница <c>long ↔ wire</c> для схемы <c>Int64String</c> из
/// <c>specs/contracts/shared.yaml</c>: наружу неотрицательный Int64 уходит
/// строкой, внутри остаётся <see cref="long"/>.
///
/// Зачем строкой: у API ровно один клиент, и JSON-число в нём — IEEE-754 double.
/// Всё, что больше 2^53−1, теряет точность молча (<c>9007199254740993</c>
/// доезжает как <c>9007199254740992</c>), и ссылка, ключ списка и следующий
/// запрос уходят на соседнюю запись.
///
/// Живёт в <c>Bugget.Api.Http</c> — общем для модулей адаптере HTTP-границы, там
/// же, где ProblemDetails и ограничения маршрута: конверсия нужна и reports,
/// и users, и analytics, и external. Ниже границы (Application, Domain, БД)
/// wire-тип не протекает.
/// </summary>
public static class WireInt64
{
    /// <summary>
    /// Текст ошибки для неканоничного сегмента адреса. Диапазон и форма записи
    /// названы явно: клиенту нужно понять, чем именно значение не подошло.
    /// </summary>
    private const string RouteValueError =
        "Ожидается неотрицательное 64-битное целое строкой: `0` либо `[1-9][0-9]*` " +
        "без знака, ведущих нулей и разделителей, в диапазоне 0..9223372036854775807.";

    /// <summary>
    /// Внутреннее значение → канон провода. Культура инвариантная явно: у
    /// культуры потока свои цифры и свой знак, а канон один на всех клиентов.
    /// </summary>
    public static string ToWire(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Канон провода → внутреннее значение. Канон уже: <c>long.TryParse</c> с
    /// <see cref="NumberStyles.None"/> пропускает <c>007</c> и цифры не-ASCII,
    /// поэтому форма записи проверяется до него, а диапазон — им.
    /// </summary>
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

    /// <summary>
    /// Разбор сегмента адреса на границе контроллера.
    /// </summary>
    /// <returns>
    /// <c>null</c>, если сегмент каноничен, — тогда <paramref name="value"/>
    /// пригоден для вызова прикладного слоя. Иначе — готовый ответ
    /// <c>400 model_state_validation_error</c>: тот же класс ошибки, который
    /// клиент получал, когда несвязываемый сегмент отбивало связывание модели.
    /// </returns>
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
