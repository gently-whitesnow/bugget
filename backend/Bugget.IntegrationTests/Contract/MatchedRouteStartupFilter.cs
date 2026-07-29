using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Отдаёт снимку контракта шаблон маршрута, которым запрос обслужен, — заголовком
/// <see cref="ContractHeaders.MatchedRoute"/>. Только в тестовом хосте: боевой пайплайн
/// этой прослойки не знает.
/// </summary>
/// <remarks>
/// Нужен из-за стабильности снимков. Путь запроса содержит идентификаторы, которые сид
/// генерирует случайно (<c>/v2/reports/294/bugs/141</c>), и снимок с ними протухал бы на
/// каждом прогоне. Шаблон (<c>/v2/reports/{aliasId}/bugs/{bugId}</c>) стабилен, и по нему
/// гейт <c>backend-contract-snapshots</c> находит операцию в контракте.
/// <para>
/// Шаблон берётся из таблицы маршрутов живого хоста, как и в <see cref="PublicSurface"/>:
/// список в коде устаревает молча, таблица — нет. Прослойка стоит снаружи маршрутизации,
/// поэтому конечная точка на момент её вызова ещё не выбрана — заголовок проставляется в
/// <c>OnStarting</c>, когда ответ уже сформирован, но ещё не отправлен.
/// </para>
/// </remarks>
internal sealed class MatchedRouteStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        builder =>
        {
            builder.Use(async (context, continuation) =>
            {
                context.Response.OnStarting(() =>
                {
                    var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        context.Response.Headers[ContractHeaders.MatchedRoute] = pattern;
                    }

                    return Task.CompletedTask;
                });

                await continuation(context);
            });

            next(builder);
        };
}
