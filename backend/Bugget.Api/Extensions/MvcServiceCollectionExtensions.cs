using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bugget.Api.Authentication;
using Bugget.Api.Binders;
using Bugget.Api.Http;
using Bugget.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Bugget.Api.Extensions;

public static class MvcServiceCollectionExtensions
{
    public static IServiceCollection AddMvcPipeline(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Фильтр по модулю, а не глобально: users и authorization объявляют авторизацию сами ([Auth] / [JwtAuth]).
            options.Conventions.Add(new ReportsModuleAuthorizationConvention());

            // Первым: иначе FileParameter уедет в BodyModelBinder — [ApiController] считает сложный тип телом.
            options.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider());

            // Тоже до штатных: enum контракта приходит строкой из `enum` OpenAPI, а не именем CLR-члена.
            options.ModelBinderProviders.Insert(1, new WireEnumModelBinderProvider());

            // Ключи ModelState — JSON-имена полей, а не CLR-имена; политика та же, что у сериализации ниже.
            options.ModelMetadataDetailsProviders.Add(
                new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.SnakeCaseLower));
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

            // Enum'ы контракта идут строкой из OpenAPI: фабрика накрывает элементы массивов,
            // модификатор — скалярные свойства с конвертером генератора (ADR-0013).
            options.JsonSerializerOptions.Converters.Add(new WireEnumJsonConverterFactory());
            options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { WireEnumJsonConverterFactory.UseWireValues }
            };
        })
        .ConfigureApiBehaviorOptions(o =>
        {
            o.InvalidModelStateResponseFactory = context => ProblemDetailsFactory.CreateValidation(context);

            // Иначе MVC превращает пустые 4xx в свой ProblemDetails без нашего `code`; пустой результат
            // должен доехать до UseProblemStatusCodes — адаптер в контуре один (ADR-0008).
            o.SuppressMapClientErrors = true;
        });

        return services;
    }
}
