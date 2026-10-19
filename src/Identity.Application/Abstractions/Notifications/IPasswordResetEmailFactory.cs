using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Notifications;

public interface IPasswordResetEmailFactory
{
    OutboxMessage Create(string email, string token, DateTimeOffset now);
}
