using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Bugget.Api.Authentication;

/// <summary>Обязательная аутентификация по схеме <see cref="AuthSchemeNames.Headers"/> только для контроллеров модуля reports.</summary>
/// <remarks>
/// Глобальный фильтр навязал бы users и authorization чужую схему. Граница модуля — namespace, а не сборка:
/// после слияния в квартет (MAIN-20) сборка модули не различает, и конвенция накрывала анонимный OIDC-callback.
/// </remarks>
public sealed class ReportsModuleAuthorizationConvention : IControllerModelConvention
{
    /// <summary>Namespace контроллеров reports; users и authorization объявляют доступ сами.</summary>
    private const string ReportsControllersNamespace = "Bugget.Api.Controllers";

    /// <summary>Та же политика для не-MVC эндпоинтов reports (MCP): конвенция достаёт только контроллеры.</summary>
    internal static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(AuthSchemeNames.Headers)
            .RequireAuthenticatedUser()
            .Build();

    private static readonly AuthorizeFilter Filter = new(Policy);

    /// <summary>Отдельная функция, а не тело <see cref="Apply"/>: ту же проверку прогоняет арх-тест поверхности доступа.</summary>
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
