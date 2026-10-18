namespace Identity.Application.Abstractions.Persistence;

public interface IDatabaseReadinessProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
