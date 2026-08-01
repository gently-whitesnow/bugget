namespace Bugget.UnitTests.Services.Attachments;

/// <summary>
/// Классы, создающие <c>VideoOptimizationMetrics</c>, выполняются последовательно.
/// <c>MeterListener</c> в проверке метрик gate фильтрует приборы по имени метра, а оно
/// одно на все экземпляры: параллельный сосед со своим метром подмешивает в тот же
/// слушатель свои queued/active, и проверка ловит чужие дельты. Это ограничение
/// process-wide слушателя, а не продукта, поэтому чиним раскладкой тестов.
/// </summary>
[CollectionDefinition(Name)]
public sealed class VideoOptimizationCollection
{
    public const string Name = "video-optimization-metrics";
}
