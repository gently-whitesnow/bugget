using System.Net;
using Bugget.Http;
using Flow.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Extensions;

public static class ErrorExtensions
{
    public static ActionResult ToProblemDetails(this Error error)
    {
        var (code, title, status) = error switch
        {
            BadRequestError e => (e.Error, e.Reason, HttpStatusCode.BadRequest),
            NotFoundError e => (e.Error, e.Reason, HttpStatusCode.NotFound),
            InternalServerError e => (e.Error, e.Reason, HttpStatusCode.InternalServerError),
            ForbiddenError e => (e.Error, e.Reason, HttpStatusCode.Forbidden),
            UnauthorizedError e => (e.Error, e.Reason, HttpStatusCode.Unauthorized),
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        };

        return ProblemDetailsFactory.Create(new ProblemDescriptor(code, title, (int)status));
    }
}
