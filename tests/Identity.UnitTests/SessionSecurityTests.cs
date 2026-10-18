using Identity.Application.Common;
using Identity.Application.Features.Auth.Logout;
using Identity.Application.Features.Auth.Refresh;
using Identity.Domain.Entities;
using Identity.Domain.Exceptions;

namespace Identity.UnitTests;

public sealed class SessionSecurityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("invalid!refresh-token-value-with-enough-characters")]
    public async Task InvalidRefreshInputIsAlwaysTheSameUnauthorizedError(string? token)
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => new RefreshValidator().Validate(new RefreshCommand(token!), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshValidatorAcceptsBase64UrlAndHonorsCancellation()
    {
        var token = new string('a', 43);
        Assert.Empty(await new RefreshValidator().Validate(new RefreshCommand(token), CancellationToken.None));
        Assert.False(OpaqueTokenText.IsSupported(new string('a', 513)));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new RefreshValidator().Validate(new RefreshCommand(token), new CancellationToken(true)));
        Assert.DoesNotContain(token, new RefreshCommand(token).ToString()!);
        Assert.DoesNotContain(token, new LogoutCommand(token).ToString()!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\naudience")]
    public void NewSessionsRequireAnExplicitValidAudience(string audience)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => Session.Create(Guid.NewGuid(), "native", null, now, now.AddDays(1), audience));
    }

    [Fact]
    public void ActiveSessionAuthorizationRejectsForeignSessionsAndInactiveUsers()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("active@identity.example", null, now);
        user.VerifyEmail(now);
        var owned = Session.Create(user.Id, "native", null, now, now.AddDays(1), "identity-native");
        SessionAuthorization.RequireActive(user, owned, now);
        var foreign = Session.Create(Guid.NewGuid(), "native", null, now, now.AddDays(1), "identity-native");
        Assert.Throws<UnauthorizedSessionException>(() => SessionAuthorization.RequireActive(user, foreign, now));
        user.Disable(now);
        Assert.Throws<UnauthorizedSessionException>(() => SessionAuthorization.RequireActive(user, owned, now));
    }
}
