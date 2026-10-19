using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Notifications;

public interface IInvitationEmailFactory
{
    OutboxMessage Create(string email, string token, DateTimeOffset now);
}
