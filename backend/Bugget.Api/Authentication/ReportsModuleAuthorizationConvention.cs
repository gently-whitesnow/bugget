using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Bugget.Api.Authentication;

/// <summary>
/// Вешает обязательную аутентификацию по схеме <see cref="AuthSchemeNames.Headers"/>
/// на контроллеры модуля reports (сборка Bugget).
/// </summary>
/// <remarks>
/// До объединения сервисов это был глобальный <c>AuthorizeFilter</c>. В одном процессе
/// с users и authorization глобальный фильтр навязал бы им чужую схему, поэтому
/// область действия ограничена сборкой хоста.
/// </remarks>
public sealed class ReportsModuleAuthorizationConvention : IControllerModelConvention
{
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

    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Assembly == typeof(ReportsModuleAuthorizationConvention).Assembly)
        {
            controller.Filters.Add(Filter);
        }
    }
}
