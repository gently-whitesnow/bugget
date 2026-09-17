namespace Bugget.Domain.Attachments;

public sealed class Attachment
{
    public required int Id { get; init; }

    /// <summary>Идентификатор сущности, к которой прикреплён файл</summary>
    public int EntityId { get; init; }

    public required int AttachType { get; init; }

    private readonly string? _storageKey;

    /// <summary>Относительный путь либо S3‑key</summary>
    public string? StorageKey
    {
        get => _storageKey;
        init => _storageKey = value;
    }

    /// <summary>Тип хранилища 0=Temp , 1=Standard, 2=Cold</summary>
    public int? StorageKind { get; init; } = 0;

    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatorUserId { get; init; }

    public long? LengthBytes { get; init; }

    public required string FileName { get; init; }

    public string MimeType { get; init; } = "image/webp";

    public bool? HasPreview { get; init; } = false;

    /// <summary>Сжато ли вложение gzip</summary>
    public bool? IsGzipCompressed { get; init; } = false;
}
