using System;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Flow.Routing;

/// <summary>
/// Возвращает ограничение типа в шаблон маршрута сгенерированного действия:
/// <c>{id}</c> → <c>{id:long}</c>.
///
/// Зачем: NSwag ограничения маршрутов не генерирует — в OpenAPI их просто нет.
/// Без ограничения нечисловой сегмент попадает в действие и падает на связывании
/// (400), тогда как раньше маршрут не совпадал и приходил 404. Это изменение
/// публичного поведения, поэтому ограничение возвращается здесь — рядом с
/// переопределением, а не форком шаблонов генератора.
///
/// Вешается точечно и только там, где ограничение было до перехода на
/// contract-first: глобальное правило «каждый int-параметр маршрута получает
/// :int» поменяло бы ответы там, где их никто не менял.
///
/// Живёт в Flow — общем для модулей наборе ASP.NET-обвязки (там же обработчик
/// невалидной модели и middleware ошибок), потому что нужен и reports, и users.
/// </summary>
/// <param name="parameter">Имя параметра маршрута, например <c>id</c>.</param>
/// <param name="constraint">Ограничение маршрутизации, например <c>long</c>.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RouteParameterConstraintAttribute(string parameter, string constraint)
    : Attribute, IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var from = $"{{{parameter}}}";
        var to = $"{{{parameter}:{constraint}}}";

        foreach (var selector in action.Selectors)
        {
            var template = selector.AttributeRouteModel?.Template;
            if (template is null || !template.Contains(from, StringComparison.Ordinal))
            {
                continue;
            }

            selector.AttributeRouteModel!.Template = template.Replace(from, to, StringComparison.Ordinal);
        }
    }
}
