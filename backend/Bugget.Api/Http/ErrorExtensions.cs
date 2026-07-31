using System.Net;
using Bugget.Api.Http;
using Bugget.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Extensions;

public static class ErrorExtensions
{
    /// <summary>
    /// Прикладная ошибка в дескриптор: код и заголовок приходят из неё самой, HTTP-статус
    /// выводится здесь, в транспортном слое. Дескриптор используется и HTTP problem+json,
    /// и realtime-адаптером.
    /// </summary>
    public static ProblemDescriptor ToDescriptor(this Error error)
    {
        var status = error switch
        {
            BadRequestError => HttpStatusCode.BadRequest,
            UnauthorizedError => HttpStatusCode.Unauthorized,
            ForbiddenError => HttpStatusCode.Forbidden,
            NotFoundError => HttpStatusCode.NotFound,
            ConflictError => HttpStatusCode.Conflict,
            InternalServerError => HttpStatusCode.InternalServerError,
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        };

        return new ProblemDescriptor(error.Code, error.Title, (int)status);
    }

    public static ActionResult ToProblemDetails(this Error error, HttpContext context) =>
        ProblemDetailsFactory.Create(context, error.ToDescriptor());
}
