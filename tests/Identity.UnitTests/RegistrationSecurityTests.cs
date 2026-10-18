using System.Diagnostics;
using System.Security.Cryptography;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Infrastructure.Email;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using VerificationResult = Identity.Application.Abstractions.Security.PasswordVerificationResult;

namespace Identity.UnitTests;

public sealed class RegistrationSecurityTests(ITestOutputHelper output)
{
    private const string Password = "Walking over the frozen lake 42!";

    [Theory]
    [InlineData("short", false)]
    [InlineData("123456789012345", false)]
    [InlineData("passwordpassword", false)]
    [InlineData("Walking over the frozen lake 42!", true)]
    [InlineData("  Walking over the frozen lake 42!  ", true)]
    [InlineData("🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀", false)]
    [InlineData("🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀🚀", true)]
    public void PasswordPolicyCountsUnicodeScalarsAndPreservesSpaces(string password, bool valid) =>
        Assert.Equal(valid, new PasswordPolicy().Validate(password).Count == 0);

    [Fact]
    public void PasswordPolicyRejectsInvalidUnicodeAndOversizedInput()
    {
        var policy = new PasswordPolicy();
        Assert.NotEmpty(policy.Validate(new string('x', 129)));
        Assert.NotEmpty(policy.Validate(new string('x', 2048)));
        Assert.NotEmpty(policy.Validate("invalid-\ud800-unicode-password"));
        Assert.NotEmpty(policy.Validate(null!));
    }

    [Fact]
    public void PasswordHasherSupportsNfkcVerificationAndRehashWithoutTrimming()
    {
        var service = new PasswordService(Options.Create(new PasswordHashOptions()));
        var watch = Stopwatch.StartNew();
        var hash = service.Hash(Password);
        watch.Stop();
        output.WriteLine("PBKDF2 600000 iterations, hash elapsed: {0:F1} ms", watch.Elapsed.TotalMilliseconds);
        Assert.Equal(VerificationResult.Success, service.Verify(hash, Password));
        Assert.Equal(VerificationResult.Failed, service.Verify(hash, " " + Password));
        Assert.Equal(VerificationResult.Failed, service.Verify("not-a-hash", Password));
        var normalizedHash = service.Hash("Ｗalking over the frozen lake 42!");
        Assert.Equal(VerificationResult.Success, service.Verify(normalizedHash, Password));
        var older = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }));
        var oldHash = older.HashPassword(new object(), Password);
        Assert.Equal(VerificationResult.SuccessRehashNeeded, service.Verify(oldHash, Password));
        Assert.False(string.Equals(hash, service.Hash(Password), StringComparison.Ordinal));
    }

    [Fact]
    public void TokensHaveHighEntropyAndOnlyTheirDigestIsPersistable()
    {
        var generator = new TokenGenerator();
        var first = generator.Generate();
        var second = generator.Generate();
        Assert.Equal(43, first.Length);
        Assert.True(first.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.False(string.Equals(first, second, StringComparison.Ordinal));
        Assert.Equal(64, generator.Hash(first).Length);
        Assert.Equal(generator.Hash(first), generator.Hash(first));
    }

    [Fact]
    public async Task AesGcmRoundTripRejectsTamperingAndSupportsRetainedKeys()
    {
        var directory = Path.Combine(Path.GetTempPath(), "identity-crypto-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var firstPath = Path.Combine(directory, "key-one");
        var secondPath = Path.Combine(directory, "key-two");
        await File.WriteAllBytesAsync(firstPath, RandomNumberGenerator.GetBytes(32));
        await File.WriteAllBytesAsync(secondPath, RandomNumberGenerator.GetBytes(32));
        try
        {
            var options = new OutboxEncryptionOptions { ActiveKeyId = "one", Keys = new() { ["one"] = firstPath, ["two"] = secondPath } };
            using var ring = new OutboxKeyRing(Options.Create(options), Options.Create(new RegistrationOptions { Enabled = true }), Options.Create(new OutboxOptions()));
            await ring.StartAsync(CancellationToken.None);
            var protector = new OutboxPayloadProtector(ring);
            const string plaintext = "encrypted email test fixture";
            var envelope = protector.Encrypt(plaintext);
            Assert.Equal(plaintext, protector.Decrypt(envelope));
            var secondEnvelope = protector.Encrypt(plaintext);
            Assert.False(string.Equals(envelope.Nonce, secondEnvelope.Nonce, StringComparison.Ordinal));
            options.ActiveKeyId = "two";
            Assert.Equal(plaintext, protector.Decrypt(envelope));
            Assert.Equal("two", protector.Encrypt(plaintext).KeyId);
            var modified = Convert.FromBase64String(envelope.Ciphertext);
            modified[0] ^= 1;
            var tampered = new Identity.Domain.ValueObjects.EncryptedEmailPayload(envelope.KeyId, envelope.Nonce,
                Convert.ToBase64String(modified), envelope.Tag);
            Assert.ThrowsAny<CryptographicException>(() => protector.Decrypt(tampered));
            var substitutedKey = new Identity.Domain.ValueObjects.EncryptedEmailPayload("two", envelope.Nonce, envelope.Ciphertext, envelope.Tag);
            Assert.ThrowsAny<CryptographicException>(() => protector.Decrypt(substitutedKey));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData("https://identity.example/verify#token={token}", SmtpTlsMode.StartTls, true, true)]
    [InlineData("http://localhost:3000/verify#token={token}", SmtpTlsMode.None, false, true)]
    [InlineData("http://localhost:3000/verify#token={token}", SmtpTlsMode.None, true, false)]
    [InlineData("https://identity.example/verify", SmtpTlsMode.StartTls, true, false)]
    public void EmailOptionsEnforceTemplatesAndProductionTls(string template, SmtpTlsMode tls, bool production, bool expected)
    {
        var options = new EmailOptions
        {
            FromAddress = "no-reply@identity.example",
            VerificationUrlTemplate = template,
            Smtp = new SmtpOptions { Host = "localhost", Tls = tls }
        };
        Assert.Equal(expected, EmailOptions.IsValid(options, production));
    }
}
