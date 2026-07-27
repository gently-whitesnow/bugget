using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Binders;

/// <summary>
/// Связывает загруженный файл из <c>multipart/form-data</c> с типом
/// <c>FileParameter</c>, который NSwag генерирует для тела с
/// <c>format: binary</c>.
///
/// Зачем: генератор эмитит свой FileParameter (поток + имя + MIME) и для
/// abstract-контроллеров тоже — сам ASP.NET такой тип связать не умеет и без
/// биндера считает его телом JSON. Без этого куска контракт вложений пришлось бы
/// не описывать вовсе, то есть оставить три контроллера вне contract-first.
///
/// Альтернатива — свой Liquid-шаблон NSwag, который эмитил бы IFormFile: это
/// форк генерации ради одного типа, а форк надо сопровождать при каждом
/// обновлении NSwag.
/// </summary>
internal sealed class FileParameterModelBinder(Func<Stream, string, string, object> factory) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            // Не multipart — оставляем модель несвязанной: дальше отработает
            // валидация, а не исключение из недр биндинга.
            return Task.CompletedTask;
        }

        // Имя параметра в контракте (`file`) — оно же имя поля формы. Если поле не
        // нашлось, берём единственный файл запроса: фронт шлёт по одному файлу,
        // а имя поля исторически не фиксировалось.
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
