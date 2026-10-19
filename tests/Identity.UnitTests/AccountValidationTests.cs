using Identity.Application.Common;
using Identity.Application.Features.Auth.RequestPasswordReset;
using Identity.Application.Features.Auth.ResetPassword;
using Identity.Application.Features.Users.ChangePassword;
using Identity.Application.Features.Users.DeactivateAccount;
using Identity.Application.Features.Users.UpdateProfile;
using Identity.Infrastructure.Email;

namespace Identity.UnitTests;

public sealed class AccountValidationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("  Name  ", true)]
    [InlineData("", false)]
    [InlineData(" \t ", false)]
    [InlineData("Name\n", false)]
    public async Task ProfileValidatorAllowsExplicitNullAndRejectsBlankOrControlCharacters(string? value, bool valid)
    {
        Assert.Equal(valid, (await new UpdateProfileValidator().Validate(new(Guid.NewGuid(), Guid.NewGuid(), value), CancellationToken.None)).Count == 0);
    }

    [Fact]
    public async Task ProfileValidatorCapsDisplayNameLengthAndHonorsCancellation()
    {
        var validator = new UpdateProfileValidator();
        Assert.NotEmpty(await validator.Validate(new(Guid.NewGuid(), Guid.NewGuid(), new string('x', 121)), CancellationToken.None));
        await Assert.ThrowsAsync<OperationCanceledException>(() => validator.Validate(new(Guid.NewGuid(), Guid.NewGuid(), "Name"), new CancellationToken(true)));
    }

    [Fact]
    public async Task PasswordValidatorsUseNewPasswordPolicyAndKeepCurrentPasswordRulesSeparate()
    {
        var validator = new ChangePasswordValidator(new PasswordPolicy());
        var valid = new ChangePasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "short legacy", "A very personal new passphrase 37!");
        Assert.Empty(await validator.Validate(valid, CancellationToken.None));
        var invalid = new ChangePasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "", "short");
        var fields = (await validator.Validate(invalid, CancellationToken.None)).Select(error => error.Field).ToArray();
        Assert.Contains("currentPassword", fields);
        Assert.Contains("newPassword", fields);
        Assert.NotEmpty(PasswordInput.Validate(new string('x', 1025), "password"));
        Assert.NotEmpty(PasswordInput.Validate("broken-\ud800", "password"));
        Assert.Empty(await new DeactivateAccountValidator().Validate(new(Guid.NewGuid(), Guid.NewGuid(), "short legacy"), CancellationToken.None));
    }

    [Fact]
    public async Task ResetValidatorKeepsUniformTokenErrorAndCorrectPasswordField()
    {
        var validator = new ResetPasswordValidator(new PasswordPolicy());
        await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(() => validator.Validate(new("bad", "valid personal passphrase 55!"), CancellationToken.None));
        var errors = await validator.Validate(new(new string('x', 43), "short"), CancellationToken.None);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal("newPassword", error.Field));
        await Assert.ThrowsAsync<OperationCanceledException>(() => validator.Validate(new("", ""), new CancellationToken(true)));
        Assert.NotEmpty(await new RequestPasswordResetValidator().Validate(new("bad\r\nemail"), CancellationToken.None));
    }

    [Theory]
    [InlineData("https://identity.example/reset#token={token}", true, true)]
    [InlineData("http://localhost:3000/reset#token={token}", false, true)]
    [InlineData("http://localhost:3000/reset#token={token}", true, false)]
    [InlineData("https://identity.example/{token}/{token}", true, false)]
    [InlineData("https://identity.example/{redirect}", true, false)]
    [InlineData("https://user:secret@identity.example/{token}", true, false)]
    public void ResetTemplatesUseTheSameStrictUrlValidation(string template, bool production, bool expected) =>
        Assert.Equal(expected, EmailOptions.IsValidTemplate(template, production));

    [Fact]
    public void SecretBearingCommandsDoNotPrintPasswordsOrResetTokens()
    {
        Assert.DoesNotContain("current-secret", new ChangePasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "current-secret", "new-secret").ToString()!);
        Assert.DoesNotContain("new-secret", new ResetPasswordCommand("token-secret", "new-secret").ToString()!);
        Assert.DoesNotContain("token-secret", new ResetPasswordCommand("token-secret", "new-secret").ToString()!);
        Assert.DoesNotContain("password-secret", new DeactivateAccountCommand(Guid.NewGuid(), Guid.NewGuid(), "password-secret").ToString()!);
    }
}
