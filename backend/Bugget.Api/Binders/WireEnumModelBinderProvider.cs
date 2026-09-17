using Bugget.Api.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bugget.Api.Binders;

/// <summary>
/// Связывание enum'ов провода из query. Штатный биндер разбирает имя CLR-члена и
/// делает это без учёта регистра, то есть принял бы и <c>Tg_beta_tester</c>, и
/// <c>BACKLOG</c> — значения, которых в контракте нет. Здесь разбор тот же, что и
/// у JSON: строка из <c>enum</c> контракта, точное совпадение, иначе 400.
///
/// Массивы фильтров этот биндер накрывает через штатный collection-биндер: тот
/// берёт биндер элемента из этой же цепочки.
/// </summary>
internal sealed class WireEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return WireEnum.IsWireEnum(type) ? new WireEnumModelBinder(WireEnum.Map(type)) : null;
    }
}
