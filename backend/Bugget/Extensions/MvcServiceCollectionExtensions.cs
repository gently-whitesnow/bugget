using System.Text.Json;
using Bugget.Authentication;
using Bugget.Binders;
using Bugget.Middlewares;

namespace Bugget.Extensions;

/// <summary>
/// Настройка MVC: конвенции, биндеры, сериализация, формат ответа на невалидную
/// модель. Вынесено из <see cref="ServiceCollectionExtensions"/> — тот файл и так
/// связан с половиной решения, и каждая новая настройка тянула туда ещё один using.
/// </summary>
public static class MvcServiceCollectionExtensions
{
    public static IServiceCollection AddMvcPipeline(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Модуль reports исторически требует аутентификации на всех своих контроллерах.
            // Контроллеры модулей users и authorization живут в том же процессе и объявляют
            // авторизацию сами ([Auth] / [JwtAuth]), поэтому фильтр вешается по сборке,
            // а не глобально.
            options.Conventions.Add(new ReportsModuleAuthorizationConvention());

            // Первым: параметр сгенерированного типа FileParameter иначе уедет в
            // BodyModelBinder — [ApiController] выводит источник сложного типа как тело.
            options.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider());
        })
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower; })
        .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = _ => new ModelStateInvalidHandler());

        return services;
    }
}
