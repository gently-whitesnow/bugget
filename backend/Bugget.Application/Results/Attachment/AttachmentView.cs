namespace Bugget.Application.Results.Attachment;

public sealed class AttachmentView
{
    public required int Id { get; init; }

    /// <summary>Идентификатор сущности к которой прикреплен файл</summary>
    public required int EntityId { get; init; }

    /// <summary>Тип сущности к которой прикреплен файл</summary>
    public required int AttachType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string CreatorUserId { get; init; }

    public required string FileName { get; init; }

    public required bool HasPreview { get; init; }
}
