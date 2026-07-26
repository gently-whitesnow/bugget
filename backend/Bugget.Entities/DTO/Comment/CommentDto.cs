using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.Comment;

public sealed class CommentDto
{
    [StringLength(2048, MinimumLength = 1)]
    public required string Text { get; init; }

    /// <summary>
    /// 0 = Internal (team-only, default), 1 = External (пересылается тестеру).
    /// Пропущенное поле трактуется как Internal.
    /// </summary>
    [Range(0, 1)]
    public short? Audience { get; init; }
}
