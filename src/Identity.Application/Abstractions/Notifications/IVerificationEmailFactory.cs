using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Notifications;

public interface IVerificationEmailFactory
{
    OutboxMessage Create(string email, string token, DateTimeOffset now);
}
