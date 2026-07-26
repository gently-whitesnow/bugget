using System.Net;
using Monade;
using Monade.Errors;

namespace Bugget.Extensions
{
    public static class ErrorExtensions
    {
        public static int ExtractStatusCode(this Error error) => (int)(error switch
        {
            BadRequestError => HttpStatusCode.BadRequest,
            NotFoundError => HttpStatusCode.NotFound,
            ConflictError => HttpStatusCode.Conflict,
            InternalServerError => HttpStatusCode.InternalServerError,
            _ => throw new NotImplementedException("Данный тип ошибки не определен")
        });
    }
}
