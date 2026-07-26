using System;
using System.Net;
using Flow.Errors;

namespace Flow.Extensions
{
    public static class ErrorExtensions
    {
        public static int ExtractStatusCode(this Error error) => (int)(error switch
        {
            BadRequestError => HttpStatusCode.BadRequest,
            NotFoundError => HttpStatusCode.NotFound,
            InternalServerError => HttpStatusCode.InternalServerError,
            ForbiddenError => HttpStatusCode.Forbidden,
            UnauthorizedError => HttpStatusCode.Unauthorized,
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        });
    }
}
