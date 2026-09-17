using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Binders;

/// <summary>
/// Связывает файл из <c>multipart/form-data</c> с <c>FileParameter</c>, который NSwag генерирует для
/// <c>format: binary</c>: ASP.NET сам этот тип не связывает и считает телом JSON. Свой Liquid-шаблон
/// с IFormFile отвергнут — форк генерации пришлось бы сопровождать при каждом обновлении NSwag.
/// </summary>
internal sealed class FileParameterModelBinder(Func<Stream, string, string, object> factory) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            // Не multipart — оставляем модель несвязанной: отработает валидация, а не исключение биндинга.
            return Task.CompletedTask;
        }

        // Поле ищем по имени параметра контракта (`file`); иначе берём единственный файл запроса:
        // имя поля исторически не фиксировалось.
        var files = request.Form.Files;
        var file = files.GetFile(bindingContext.FieldName) ?? (files.Count == 1 ? files[0] : null);
        if (file is null)
        {
            return Task.CompletedTask;
        }

        var model = factory(file.OpenReadStream(), file.FileName, file.ContentType);
        bindingContext.Result = ModelBindingResult.Success(model);
        return Task.CompletedTask;
    }
}
