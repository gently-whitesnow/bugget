using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Binders;

/// <summary>
/// Массив <c>format: binary</c> в <c>multipart/form-data</c>: NSwag эмитит
/// <c>IEnumerable&lt;FileParameter&gt;</c>, а поле формы повторяется для каждого файла.
/// </summary>
internal sealed class FileParameterCollectionModelBinder(
    Type elementType,
    Func<Stream, string, string, object> factory) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            return Task.CompletedTask;
        }

        var files = request.Form.Files.GetFiles(bindingContext.FieldName);
        var model = Array.CreateInstance(elementType, files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            model.SetValue(factory(files[i].OpenReadStream(), files[i].FileName, files[i].ContentType), i);
        }

        bindingContext.Result = ModelBindingResult.Success(model);
        return Task.CompletedTask;
    }
}
