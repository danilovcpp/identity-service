namespace Identity.Application.Common;

public sealed class DuplicateEmailException() : Exception("A user with this identifier already exists.");
