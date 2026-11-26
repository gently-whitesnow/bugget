namespace Bugget.Application.Options;

public sealed class FileStorageOptions
{
    public required string BaseDirectory { get; set; }

    /// <summary>Сколько ждать свободный слот и сколько ждать саму операцию с диском.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Сколько операций с диском идёт одновременно; столько потоков займёт зависшее хранилище.</summary>
    public int MaxConcurrentOperations { get; set; } = 8;
}
