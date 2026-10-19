using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;

namespace Identity.Application.Features.Admin.Auth.GetMe;

public sealed record GetAdminMeQuery(Guid UserId) : IQuery<AdminProfile>;
