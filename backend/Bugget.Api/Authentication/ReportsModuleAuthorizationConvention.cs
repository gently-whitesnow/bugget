using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Bugget.Api.Authentication;

/// <summary>
/// Вешает обязательную аутентификацию по схеме <see cref="AuthSchemeNames.Headers"/>
/// на контроллеры модуля reports.
/// </summary>
/// <remarks>
/// До объединения сервисов это был глобальный <c>AuthorizeFilter</c>. В одном процессе
/// с users и authorization глобальный фильтр навязал бы им чужую схему, поэтому область
/// действия ограничена модулем reports. Границей модуля была сборка, пока модули были
/// отдельными проектами; после слияния в квартет (MAIN-20) сборка перестала их различать
/// и конвенция накрыла в том числе анонимный OIDC-callback — новые пользователи не
/// заводились вовсе. Поэтому граница задана пространством имён модуля.
/// </remarks>
public sealed class ReportsModuleAuthorizationConvention : IControllerModelConvention
{
    /// <summary>
    /// Пространство имён контроллеров модуля reports. Контроллеры users и authorization
    /// лежат в <c>Bugget.Api.Users.*</c> и <c>Bugget.Api.Authorization.*</c> и объявляют
    /// доступ сами.
    /// </summary>
    private const string ReportsControllersNamespace = "Bugget.Api.Controllers";

    /// <summary>
    /// Та же политика для не-MVC эндпоинтов модуля reports (MCP): конвенция достаёт
    /// только контроллеры, а требование к доступу у всей поверхности одно.
    /// </summary>
    internal static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(AuthSchemeNames.Headers)
            .RequireAuthenticatedUser()
            .Build();

    private static readonly AuthorizeFilter Filter = new(Policy);

    /// <summary>
    /// Принадлежность контроллера модулю reports. Отдельная функция, а не тело
    /// <see cref="Apply"/>: ту же проверку прогоняет арх-тест поверхности доступа.
    /// </summary>
    public static bool BelongsToReportsModule(Type controllerType) =>
        controllerType.Namespace is { } ns
        && (ns.Equals(ReportsControllersNamespace, StringComparison.Ordinal)
            || ns.StartsWith(ReportsControllersNamespace + ".", StringComparison.Ordinal));

    public void Apply(ControllerModel controller)
    {
        if (BelongsToReportsModule(controller.ControllerType))
        {
            controller.Filters.Add(Filter);
        }
    }
}
