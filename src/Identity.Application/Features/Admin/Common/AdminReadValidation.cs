using System.Globalization;
using Identity.Application.Common;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Common;

public static class AdminReadValidation
{
    public static void Page(int page, int pageSize, List<FieldValidationError> errors)
    {
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
        { errors.Add(new("page", "page_invalid", "Use a positive page with an offset within the supported range.")); }
        if (pageSize is < 1 or > 100)
        { errors.Add(new("pageSize", "page_size_invalid", "Use a page size between 1 and 100.")); }
    }

    public static void Identifier(Guid id, List<FieldValidationError> errors, string field = "userId")
    {
        if (id == Guid.Empty) { errors.Add(new(field, "identifier_invalid", "A nonempty identifier is required.")); }
    }

    public static bool TryStatus(string? text, out UserStatus status)
    {
        status = text switch
        {
            nameof(UserStatus.PendingVerification) => UserStatus.PendingVerification,
            nameof(UserStatus.Active) => UserStatus.Active,
            nameof(UserStatus.Disabled) => UserStatus.Disabled,
            nameof(UserStatus.Deactivated) => UserStatus.Deactivated,
            nameof(UserStatus.Invited) => UserStatus.Invited,
            _ => (UserStatus)(-1)
        };
        return Enum.IsDefined(status);
    }

    public static bool TryUtcInstant(string text, out DateTimeOffset value)
    {
        value = default;
        var explicitOffset = text.EndsWith('Z') || (text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':');
        if (!explicitOffset || !DateTimeOffset.TryParseExact(text,
            ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)) { return false; }
        value = parsed.ToUniversalTime();
        return true;
    }
}
