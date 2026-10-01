using Bugget.Api.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Binders;

internal sealed class WireEnumModelBinder(WireEnumMap map) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var provided = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (provided == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, provided);

        if (map.TryParse(provided.FirstValue, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"Ожидалось одно из значений: {map.AllowedValues}.");

        return Task.CompletedTask;
    }
}
