using System.Diagnostics;

namespace Identity.Application.Common;

public sealed class OperationContext
{
    public string CorrelationId { get; } = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
}
