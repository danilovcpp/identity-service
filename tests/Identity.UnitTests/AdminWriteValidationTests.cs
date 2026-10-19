using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Users.DisableUser;
using Identity.Application.Features.Admin.Users.InviteUser;
using Identity.Application.Features.Auth.AcceptInvitation;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Exceptions;
using Identity.Infrastructure.Email;

namespace Identity.UnitTests;

public sealed class AdminWriteValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Invited)]
    public void ManualDisableEnableRestoresExactStateWithoutVerifyingEmail(UserStatus status)
    {
        var user = status == UserStatus.Invited ? User.CreateInvited("user@identity.example", null, Now) : User.Create("user@identity.example", null, Now);
        if (status == UserStatus.Active) { user.VerifyEmail(Now); }
        var verified = user.EmailVerifiedAt;
        var actor = Guid.NewGuid();
        user.Disable(Now.AddMinutes(1), actor, "  Operator reason  ");
        Assert.Equal(status, user.StatusBeforeDisable);
        Assert.Equal(actor, user.DisabledBy);
        Assert.Equal("Operator reason", user.DisableReason);
        Assert.Throws<DomainException>(() => user.AcceptInvitation(Now.AddMinutes(1)));
        user.Enable(Now.AddMinutes(2));
        Assert.Equal(status, user.Status);
        Assert.Equal(verified, user.EmailVerifiedAt);
        Assert.Null(user.StatusBeforeDisable);
        Assert.Null(user.DisabledAt);
        Assert.Null(user.DisabledBy);
        Assert.Null(user.DisableReason);
    }

    [Fact]
    public void InvitationActivationConfirmsEmailOnceAndDeactivatedUsersCannotBeEnabled()
    {
        var user = User.CreateInvited("user@identity.example", null, Now);
        user.AcceptInvitation(Now.AddMinutes(1));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(Now.AddMinutes(1), user.EmailVerifiedAt);
        Assert.Throws<DomainException>(() => user.AcceptInvitation(Now.AddMinutes(1)));
        user.Deactivate(Now.AddMinutes(2));
        Assert.Throws<DomainException>(() => user.Enable(Now.AddMinutes(3)));
        Assert.Throws<DomainException>(() => user.Disable(Now.AddMinutes(3)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("reason\ninvalid")]
    public async Task DisableRequiresValidReason(string reason)
    {
        Assert.NotEmpty(await new DisableUserValidator().Validate(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason), CancellationToken.None));
    }

    [Fact]
    public async Task CommandsValidateIdentifiersEmailDisplayNameAndBoundedTextWithoutPrintingInputs()
    {
        var invite = new InviteUserCommand(Guid.Empty, Guid.Empty, "bad\nemail", new string('x', 121));
        Assert.Equal(4, (await new InviteUserValidator().Validate(invite, CancellationToken.None)).Count);
        var disable = new DisableUserCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('r', 501));
        Assert.Single(await new DisableUserValidator().Validate(disable, CancellationToken.None));
        Assert.DoesNotContain(disable.Reason, disable.ToString()!);
        Assert.DoesNotContain(invite.Email, invite.ToString()!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InviteUserValidator().Validate(invite, new CancellationToken(true)));
    }

    [Fact]
    public async Task AcceptanceUsesExistingPasswordPolicyAndSafeInvalidTokenError()
    {
        var validator = new AcceptInvitationValidator(new PasswordPolicy());
        await Assert.ThrowsAsync<InvalidInvitationTokenException>(() => validator.Validate(new("bad", "short"), CancellationToken.None));
        Assert.NotEmpty(await validator.Validate(new(new string('a', 43), "short"), CancellationToken.None));
        var command = new AcceptInvitationCommand(new string('a', 43), "Walking over the frozen lake 42!");
        Assert.Empty(await validator.Validate(command, CancellationToken.None));
        Assert.DoesNotContain(command.Token, command.ToString()!);
        Assert.DoesNotContain(command.NewPassword, command.ToString()!);
    }

    [Theory]
    [InlineData("https://identity.example/account/activate#token={token}", true)]
    [InlineData("https://identity.example/account/activate?token={token}", false)]
    [InlineData("https://identity.example/{token}#token=x", false)]
    [InlineData("https://identity.example/account/activate#token={token}&extra={token}", false)]
    [InlineData("http://identity.example/account/activate#token={token}", false)]
    public void SensitiveLinksRequireHttpsAndTokenOnlyInFragment(string template, bool valid) =>
        Assert.Equal(valid, EmailOptions.IsValidFragmentTemplate(template, production: true));
}
