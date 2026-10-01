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
        if (IsFileParameter(type))
        {
            return CreateFactory(type) is { } single ? new FileParameterModelBinder(single) : null;
        }

        var elementType = context.Metadata.ElementType;
        if (elementType is null || !IsFileParameter(elementType) || !type.IsAssignableFrom(elementType.MakeArrayType()))
        {
            return null;
        }

        return CreateFactory(elementType) is { } many ? new FileParameterCollectionModelBinder(elementType, many) : null;
    }

    private static bool IsFileParameter(Type type) => type.Name == "FileParameter";

    private static Func<Stream, string, string, object>? CreateFactory(Type type)
    {
        var constructor = type.GetConstructor([typeof(Stream), typeof(string), typeof(string)]);
        if (constructor is null)
        {
            return null;
        }

        return (data, fileName, contentType) => constructor.Invoke([data, fileName, contentType]);
    }
}
