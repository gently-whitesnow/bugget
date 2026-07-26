using JetBrains.Annotations;

namespace Flow.Errors;

[PublicAPI]
public record NotFoundError(string Error, string Reason) : Error;
