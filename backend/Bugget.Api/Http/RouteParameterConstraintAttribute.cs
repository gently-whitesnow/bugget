using System;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bugget.Api.Http;

/// <summary>
/// Возвращает ограничение типа в шаблон маршрута сгенерированного действия: <c>{id}</c> → <c>{id:long}</c>.
/// NSwag ограничений не генерирует, а без них нечисловой сегмент даёт 400 вместо прежнего 404 —
/// это изменение публичного поведения. Вешается точечно, только там, где ограничение было
/// до contract-first: глобальное правило поменяло бы ответы там, где их никто не менял.
/// </summary>
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
