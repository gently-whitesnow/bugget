using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Binders;

/// <summary>
/// Отдаёт <see cref="FileParameterModelBinder"/> для сгенерированных типов
/// <c>FileParameter</c>. Тип ищется по форме, а не по полному имени: NSwag
/// эмитит его в namespace каждого модуля, и хардкод одного имени сломался бы
/// на втором модуле с загрузкой файла.
///
/// Провайдер обязан стоять первым: иначе параметр перехватит BodyModelBinder —
/// <c>[ApiController]</c> выводит источник сложного типа как тело JSON.
/// </summary>
internal sealed class FileParameterModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.Metadata.ModelType;
        if (type.Name != "FileParameter")
        {
            return null;
        }

        var constructor = type.GetConstructor([typeof(Stream), typeof(string), typeof(string)]);
        if (constructor is null)
        {
            return null;
        }

        return new FileParameterModelBinder((data, fileName, contentType) =>
            constructor.Invoke([data, fileName, contentType]));
    }
}
