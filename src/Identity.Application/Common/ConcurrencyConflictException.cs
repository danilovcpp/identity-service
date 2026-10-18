namespace Identity.Application.Common;

public sealed class ConcurrencyConflictException() : Exception("The state changed concurrently.");
