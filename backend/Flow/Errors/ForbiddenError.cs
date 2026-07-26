using JetBrains.Annotations;

namespace Flow.Errors;

[PublicAPI]
public record ForbiddenError(string Error, string Reason) : Error;
