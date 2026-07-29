using System.Text.Json;
using Bugget.Authentication;
using Bugget.Binders;
using Bugget.Http;
using Bugget.Middlewares;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

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

            // Ключи ModelState по умолчанию — CLR-имена свойств, и клиент получал бы
            // `Scopes[0].Key` вместо поля, которое сам отправил. Провайдер подставляет
            // JSON-имя (JsonPropertyName, иначе политика) на каждом сегменте пути, включая
            // вложенные объекты и элементы массивов; индексы и ключи query/route он не
            // трогает. Политика та же, что у сериализации ниже, — второго источника имён нет.
            options.ModelMetadataDetailsProviders.Add(
                new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.SnakeCaseLower));
        })
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower; })
        .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
            ProblemDetailsFactory.CreateValidation(context));

        return services;
    }
}
