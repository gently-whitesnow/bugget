using JetBrains.Annotations;

namespace Flow.Errors;

[PublicAPI]
public record InternalServerError(string Error, string Reason) : Error;
