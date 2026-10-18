using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.Refresh;

public sealed class RefreshCommand(string refreshToken) : ICommand<TokenPairResult>
{
    public string RefreshToken { get; } = refreshToken;
}
