using System.Net;
using Bugget.Http;
using Microsoft.AspNetCore.Mvc;
using Monade;
using Monade.Errors;

namespace Bugget.Extensions;

public static class ErrorExtensions
{
    public static ActionResult ToProblemDetails(this Error error)
    {
        var (code, title, status) = error switch
        {
            BadRequestError e => (e.Error, e.Reason, HttpStatusCode.BadRequest),
            NotFoundError e => (e.Error, e.Reason, HttpStatusCode.NotFound),
            ConflictError e => (e.Error, e.Reason, HttpStatusCode.Conflict),
            InternalServerError e => (e.Error, e.Reason, HttpStatusCode.InternalServerError),
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        };

        return ProblemDetailsFactory.Create(new ProblemDescriptor(code, title, (int)status));
    }
}
