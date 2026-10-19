namespace Identity.Application.Common;

public sealed class AdminUserConflictException : Exception;
public sealed class AdminEmailConflictException : Exception;
public sealed class InvitationUnavailableException : Exception;
public sealed class PasswordResetUnavailableException : Exception;
public sealed class InvalidInvitationTokenException : Exception;
