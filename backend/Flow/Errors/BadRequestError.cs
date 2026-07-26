using JetBrains.Annotations;

namespace Flow.Errors;

[PublicAPI]
public record BadRequestError(string Error, string Reason) : Error;
