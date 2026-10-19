using Identity.Application.Common;

namespace Identity.Application.Features.Admin.Common;

public static class AdminWriteValidation
{
    public static List<FieldValidationError> Identifiers(Guid actorId, Guid actorSessionId, Guid? targetId = null)
    {
        var errors = new List<FieldValidationError>();
        AdminReadValidation.Identifier(actorId, errors, "actorId");
        AdminReadValidation.Identifier(actorSessionId, errors, "actorSessionId");
        if (targetId is { } target) { AdminReadValidation.Identifier(target, errors); }
        return errors;
    }
}
