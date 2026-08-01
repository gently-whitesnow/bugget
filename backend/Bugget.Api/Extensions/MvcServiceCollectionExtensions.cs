using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bugget.Api.Authentication;
using Bugget.Api.Binders;
using Bugget.Api.Http;
using Bugget.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Bugget.Api.Extensions;

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

            // Тоже до штатных: enum контракта приходит в query строкой из `enum`
            // OpenAPI, а не именем CLR-члена, и разбор у него строгий.
            options.ModelBinderProviders.Insert(1, new WireEnumModelBinderProvider());

            // Ключи ModelState по умолчанию — CLR-имена свойств, и клиент получал бы
            // `Scopes[0].Key` вместо поля, которое сам отправил. Провайдер подставляет
            // JSON-имя (JsonPropertyName, иначе политика) на каждом сегменте пути, включая
            // вложенные объекты и элементы массивов; индексы и ключи query/route он не
            // трогает. Политика та же, что у сериализации ниже, — второго источника имён нет.
            options.ModelMetadataDetailsProviders.Add(
                new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.SnakeCaseLower));
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

            // Enum'ы контракта уходят на провод строкой из `enum` OpenAPI. Фабрика
            // накрывает элементы массивов, модификатор — скалярные свойства, на
            // которых генератор уже поставил свой конвертер (ADR-0012).
            options.JsonSerializerOptions.Converters.Add(new WireEnumJsonConverterFactory());
            options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { WireEnumJsonConverterFactory.UseWireValues }
            };
        })
        .ConfigureApiBehaviorOptions(o =>
        {
            o.InvalidModelStateResponseFactory = context => ProblemDetailsFactory.CreateValidation(context);

            // MVC сам превращает пустые 4xx (`NotFound()`, `Unauthorized()`, 415) в свой
            // ProblemDetails — с чужим `type` и без нашего `code`. Это третья форма ошибки
            // на проводе. Отключаем: пустой результат доезжает до UseProblemStatusCodes,
            // и адаптер в контуре остаётся один (ADR-0008).
            o.SuppressMapClientErrors = true;
        });

        return services;
    }
}
