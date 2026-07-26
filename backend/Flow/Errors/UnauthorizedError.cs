namespace Flow.Errors;

public record UnauthorizedError(string Error, string Reason) : Error;
