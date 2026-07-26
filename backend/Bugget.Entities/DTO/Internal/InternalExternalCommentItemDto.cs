namespace Bugget.Entities.DTO.Internal;

/// <summary>
/// DTO внешнего (tester-visible) комментария. I-1: поле <c>audience</c> в
/// контракт не проектируется — внешний потребитель (beta-bot) не должен
/// видеть атрибут «internal/external».
/// </summary>
public sealed class InternalExternalCommentItemDto
{
    public required int Id { get; init; }
    public required int BugId { get; init; }
    public required string Text { get; init; }
    public required int CreatorType { get; init; }
    public required string CreatorUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
