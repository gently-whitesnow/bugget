using System.Net;
using Bugget.Entities.Errors;
using Bugget.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Extensions;

public static class ErrorExtensions
{
    /// <summary>
    /// Доменная ошибка в дескриптор: код и заголовок приходят из неё самой, HTTP-статус
    /// выводится здесь, в транспортном слое. Дескриптор транспорта не знает, поэтому из
    /// него собирается и HTTP problem+json, и payload realtime-канала.
    /// </summary>
    public static ProblemDescriptor ToDescriptor(this Error error)
    {
        var (code, title, status) = error switch
        {
            BadRequestError e => (e.Error, e.Reason, HttpStatusCode.BadRequest),
            NotFoundError e => (e.Error, e.Reason, HttpStatusCode.NotFound),
            ConflictError e => (e.Error, e.Reason, HttpStatusCode.Conflict),
            InternalServerError e => (e.Error, e.Reason, HttpStatusCode.InternalServerError),
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        };

        return new ProblemDescriptor(code, title, (int)status);
    }

    public static ActionResult ToProblemDetails(this Error error, HttpContext context) =>
        ProblemDetailsFactory.Create(context, error.ToDescriptor());
}
