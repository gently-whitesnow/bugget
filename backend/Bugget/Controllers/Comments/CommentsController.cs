using Bugget.BO.Services.Comments;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.Comment;
using Bugget.Entities.DTO.Comment;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Comments;

/// <summary>
/// Api для работы с комментами
/// </summary>
[Route("/v2/reports/{aliasId}/bugs/{bugId}/comments")]
public sealed class CommentsController(
    CommentsService commentsService) : ApiController
{
    /// <summary>
    /// Добавить комментарий
    /// </summary>
    /// <param name="aliasId"></param>
    /// <param name="bugId"></param>
    /// <param name="createDto"></param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(CommentSummaryDbModel), 201)]
    public async Task<IActionResult> CreateCommentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromBody] CommentDto createDto)
    {
        var user = User.GetIdentity();
        return await commentsService.CreateCommentAsync(user, aliasId, bugId, createDto).AsActionResultAsync(201);
    }

    /// <summary>
    /// Удалить свой комментарий
    /// </summary>
    /// <param name="aliasId"></param>
    /// <param name="bugId"></param>
    /// <param name="commentId"></param>
    /// <returns></returns>
    [HttpDelete("{commentId}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteCommentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int commentId)
    {
        var user = User.GetIdentity();
        return commentsService.DeleteCommentAsync(user, aliasId, bugId, commentId).AsActionResultAsync();
    }

    /// <summary>
    /// Обновить свой комментарий
    /// </summary>
    /// <param name="aliasId"></param>
    /// <param name="bugId"></param>
    /// <param name="commentId"></param>
    /// <param name="updateDto"></param>
    /// <returns></returns>
    [HttpPut("{commentId}")]
    [ProducesResponseType(typeof(CommentSummaryDbModel), 200)]
    public Task<IActionResult> UpdateCommentAsync([FromRoute] string aliasId, [FromRoute] int bugId, [FromRoute] int commentId, [FromBody] CommentDto updateDto)
    {
        var user = User.GetIdentity();
        return commentsService.UpdateCommentAsync(user, aliasId, bugId, commentId, updateDto).AsActionResultAsync();
    }
}
