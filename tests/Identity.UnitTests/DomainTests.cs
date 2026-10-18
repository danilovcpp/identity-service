using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Exceptions;
using Identity.Domain.ValueObjects;

namespace Identity.UnitTests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(" User@Example.com ", "USER@EXAMPLE.COM")]
    [InlineData("first.last+tag@gmail.com", "FIRST.LAST+TAG@GMAIL.COM")]
    public void EmailNormalizationIsInvariantAndDoesNotApplyProviderRules(string email, string expected) =>
        Assert.Equal(expected, EmailAddress.Normalize(email));

    [Theory]
    [InlineData("user@example.com\r\n")]
    [InlineData("us\ter@example.com")]
    [InlineData("User <user@example.com>")]
    [InlineData("not-an-address")]
    [InlineData("")]
    public void InvalidEmailIsRejected(string email) =>
        Assert.Throws<DomainException>(() => User.Create(email, null, Now));

    [Fact]
    public void UserTransitionsPreserveVerificationAndTerminalDeactivation()
    {
        var user = User.Create("User@Example.com", " Example ", Now);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal("User@Example.com", user.Email);
        Assert.Equal("Example", user.DisplayName);
        Assert.Null(user.EmailVerifiedAt);
        user.VerifyEmail(Now.AddMinutes(1));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(Now.AddMinutes(1), user.EmailVerifiedAt);
        Assert.Throws<DomainException>(() => user.VerifyEmail(Now.AddMinutes(2)));
        user.Disable(Now.AddMinutes(2));
        Assert.Equal(UserStatus.Disabled, user.Status);
        user.Deactivate(Now.AddMinutes(3));
        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.Equal(Now.AddMinutes(3), user.DeactivatedAt);
        Assert.Throws<DomainException>(() => user.SetDisplayName("Changed", Now.AddMinutes(4)));
        Assert.Throws<DomainException>(() => user.VerifyEmail(Now.AddMinutes(4)));
    }

    [Fact]
    public void FailedUserTransitionDoesNotChangeState()
    {
        var user = User.Create("user@example.com", null, Now);
        Assert.Throws<DomainException>(() => user.VerifyEmail(Now.AddTicks(-1)));
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Throws<DomainException>(() => user.SetDisplayName(new string('x', 121), Now));
        user.VerifyEmail(Now);
        user.RecordFailedLogin(Now, Now.AddMinutes(5));
        Assert.Equal(1, user.FailedLoginCount);
        user.ResetLoginFailures(Now);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void CredentialSeparatesHashAndMaintainsChangeTime()
    {
        var credential = Credential.Create(Guid.NewGuid(), "encoded-hash-fixture", Now);
        credential.ChangePasswordHash("changed-encoded-hash-fixture", Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), credential.PasswordChangedAt);
        Assert.Equal("changed-encoded-hash-fixture", credential.PasswordHash);
        Assert.Throws<DomainException>(() => credential.ChangePasswordHash("", Now));
    }

    [Fact]
    public void SessionsUseUtcSanitizeAgentAndExpireAtTheBoundary()
    {
        var clock = new ManualTimeProvider(Now);
        var session = Session.Create(Guid.NewGuid(), "native", "Agent\r\n" + new string('x', 600),
            clock.GetUtcNow().ToOffset(TimeSpan.FromHours(6)), Now.AddDays(30));
        Assert.Equal(TimeSpan.Zero, session.CreatedAt.Offset);
        Assert.Equal(512, session.UserAgent?.Length);
        Assert.DoesNotContain('\r', session.UserAgent!);
        Assert.True(session.IsActive(clock.GetUtcNow()));
        clock.Advance(TimeSpan.FromDays(30));
        Assert.False(session.IsActive(clock.GetUtcNow()));
        Assert.Throws<DomainException>(() => session.Touch(clock.GetUtcNow()));
        session.Revoke(SessionRevokeReason.Logout, clock.GetUtcNow());
        session.Revoke(SessionRevokeReason.LogoutAll, clock.GetUtcNow());
        Assert.Equal(SessionRevokeReason.Logout, session.RevokeReason);
    }

    [Fact]
    public void RefreshTokenOnlyAcceptsOneReplacementFromItsSession()
    {
        var session = Session.Create(Guid.NewGuid(), "native", null, Now, Now.AddDays(30));
        var token = RefreshToken.Create(session, new string('a', 64), Now, Now.AddDays(7));
        var replacement = RefreshToken.Create(session, new string('b', 64), Now.AddMinutes(1), Now.AddDays(7));
        var foreign = Session.Create(Guid.NewGuid(), "native", null, Now, Now.AddDays(30));
        var foreignToken = RefreshToken.Create(foreign, new string('c', 64), Now.AddMinutes(1), Now.AddDays(7));
        Assert.Throws<DomainException>(() => token.Consume(foreignToken, Now.AddMinutes(1)));
        token.Consume(replacement, Now.AddMinutes(1));
        Assert.False(token.IsUsable(Now.AddMinutes(1)));
        Assert.Equal(replacement.Id, token.ReplacedByTokenId);
        Assert.Throws<DomainException>(() => token.Consume(replacement, Now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => RefreshToken.Create(session, "raw-token", Now, Now.AddDays(1)));
        Assert.Throws<DomainException>(() => RefreshToken.Create(session, new string('a', 64), Now, Now.AddDays(8)));
        var shortSession = Session.Create(Guid.NewGuid(), "native", null, Now, Now.AddHours(1));
        Assert.Throws<DomainException>(() => RefreshToken.Create(shortSession, new string('d', 64), Now, Now.AddHours(2)));
    }

    [Theory]
    [InlineData(OneTimeTokenPurpose.EmailVerification, 1440)]
    [InlineData(OneTimeTokenPurpose.PasswordReset, 30)]
    public void OneTimeTokensAreSingleUseAndExpireAtTheirBoundary(OneTimeTokenPurpose purpose, int minutes)
    {
        var clock = new ManualTimeProvider(Now);
        var token = OneTimeToken.Create(Guid.NewGuid(), purpose, new string('a', 64), Now, Now.AddMinutes(minutes));
        token.Consume(clock.GetUtcNow());
        Assert.Throws<DomainException>(() => token.Consume(clock.GetUtcNow()));
        var expired = OneTimeToken.Create(Guid.NewGuid(), purpose, new string('b', 64), Now, Now.AddMinutes(minutes));
        clock.Advance(TimeSpan.FromMinutes(minutes));
        Assert.False(expired.IsUsable(clock.GetUtcNow()));
        Assert.Throws<DomainException>(() => expired.Consume(clock.GetUtcNow()));
        Assert.Throws<DomainException>(() => OneTimeToken.Create(Guid.NewGuid(), purpose, new string('c', 64), Now, Now.AddMinutes(minutes + 1)));
    }

    [Fact]
    public void OutboxAcceptsOnlyAnEnvelopeAndClearsItOnDelivery()
    {
        var envelope = new EncryptedEmailPayload("test-key-id", Convert.ToBase64String(new byte[12]),
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), Convert.ToBase64String(new byte[16]));
        var message = OutboxMessage.Create("email_verification", envelope, Now);
        Assert.Throws<DomainException>(() => message.RecordFailure("exception with secret", Now, Now.AddMinutes(1)));
        message.RecordFailure("smtp_unavailable", Now, Now.AddMinutes(1));
        Assert.Equal(1, message.AttemptCount);
        message.MarkProcessed(Now.AddMinutes(1));
        Assert.Null(message.Payload);
        Assert.Null(message.LastError);
        Assert.Throws<DomainException>(() => message.MarkProcessed(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => new EncryptedEmailPayload("test-key-id", "plaintext", "plaintext", "plaintext"));
    }

    [Fact]
    public void EntitiesDoNotExposePublicSetters()
    {
        var entities = new[] { typeof(User), typeof(Credential), typeof(Session), typeof(RefreshToken),
            typeof(OneTimeToken), typeof(OutboxMessage), typeof(SecurityAuditEvent) };
        foreach (var entity in entities)
        {
            Assert.DoesNotContain(entity.GetProperties(), property => property.SetMethod?.IsPublic == true);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
