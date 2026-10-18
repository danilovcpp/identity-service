using System.Security.Cryptography;
using Identity.Application.Common;
using Identity.Application.Features.Auth.Login;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Identity.UnitTests;

public sealed class LoginSecurityTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("http", false)]
    [InlineData("issuer-query", false)]
    [InlineData("lifetime-small", false)]
    [InlineData("lifetime-large", false)]
    [InlineData("skew", false)]
    [InlineData("key-id", false)]
    [InlineData("relative-path", false)]
    [InlineData("active-retained", false)]
    [InlineData("production-test", false)]
    public void JwtConfigurationHasExplicitSecurityBounds(string scenario, bool valid)
    {
        var options = new JwtOptions { Enabled = true, Issuer = "https://identity.example", ActiveKeyId = "jwt-2026", PrivateKeyPath = Path.GetFullPath("test.pem") };
        switch (scenario)
        {
            case "http": options.Issuer = "http://localhost"; break;
            case "issuer-query": options.Issuer += "?value=secret"; break;
            case "lifetime-small": options.AccessTokenLifetimeSeconds = 59; break;
            case "lifetime-large": options.AccessTokenLifetimeSeconds = 901; break;
            case "skew": options.ClockSkewSeconds = 31; break;
            case "key-id": options.ActiveKeyId = "invalid/key"; break;
            case "relative-path": options.PrivateKeyPath = "relative.pem"; break;
            case "active-retained": options.PublicKeys.Add(options.ActiveKeyId, options.PrivateKeyPath); break;
            case "production-test": options.ActiveKeyId = "test-key"; break;
        }
        Assert.Equal(valid, JwtOptions.IsValid(options, production: true));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("duplicate", false)]
    [InlineData("no-enabled", false)]
    [InlineData("empty-audience", false)]
    [InlineData("control", false)]
    public void ClientConfigurationRejectsAmbiguousOrInvalidSelectors(string scenario, bool valid)
    {
        var options = new IdentityClientOptions { Clients = [new() { ClientId = "native", Audience = "identity-native", Enabled = true }] };
        switch (scenario)
        {
            case "duplicate": options.Clients.Add(new() { ClientId = "native", Audience = "other", Enabled = true }); break;
            case "no-enabled": options.Clients[0].Enabled = false; break;
            case "empty-audience": options.Clients[0].Audience = ""; break;
            case "control": options.Clients[0].ClientId = "native\n"; break;
        }
        Assert.Equal(valid, IdentityClientOptions.IsValid(options));
    }

    [Theory]
    [InlineData("short legacy password", true)]
    [InlineData("  password with spaces  ", true)]
    [InlineData("", false)]
    public async Task LoginValidatesBoundsWithoutApplyingNewPasswordPolicy(string password, bool valid)
    {
        var errors = await new LoginValidator().Validate(new LoginCommand("user@identity.example", password, "native", null), CancellationToken.None);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public async Task LoginValidatorRejectsOversizeAndInvalidUnicodeAndHonorsCancellation()
    {
        var validator = new LoginValidator();
        Assert.NotEmpty(await validator.Validate(new LoginCommand("user@identity.example", new string('x', 1025), "native", null), CancellationToken.None));
        Assert.NotEmpty(await validator.Validate(new LoginCommand("user@identity.example", "invalid-\ud800", "native", null), CancellationToken.None));
        Assert.NotEmpty(await validator.Validate(new LoginCommand("bad\r\nemail", "some password", "", null), CancellationToken.None));
        await Assert.ThrowsAsync<OperationCanceledException>(() => validator.Validate(new LoginCommand("", "", "", null), new CancellationToken(true)));
    }

    [Theory]
    [InlineData("weak")]
    [InlineData("public-only")]
    [InlineData("malformed")]
    [InlineData("retained-private")]
    public async Task KeyRingFailsClosedForInvalidSecretMaterial(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), "identity-key-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var rsa = RSA.Create(scenario == "weak" ? 1024 : 2048);
            await File.WriteAllTextAsync(path, scenario == "malformed" ? "invalid key material" :
                scenario == "public-only" ? rsa.ExportSubjectPublicKeyInfoPem() : rsa.ExportPkcs8PrivateKeyPem());
            var options = new JwtOptions { Enabled = true, ActiveKeyId = "test-key", PrivateKeyPath = path };
            if (scenario == "retained-private") { options.PublicKeys.Add("old", path); }
            using var ring = new JwtKeyRing(Options.Create(options));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ring.StartAsync(CancellationToken.None));
            Assert.Equal("JWT key configuration could not be loaded.", error.Message);
            Assert.Null(error.InnerException);
            Assert.Empty(ring.GetPublicKeys());
            Assert.Empty(ring.Resolve("test-key"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RehashIsDistinctFromChangingThePasswordAndTokenTypesDoNotPrintSecrets()
    {
        var created = DateTimeOffset.UtcNow;
        var credential = Credential.Create(Guid.NewGuid(), "old-hash", created);
        credential.Rehash("upgraded-hash");
        Assert.Equal(created, credential.PasswordChangedAt);
        Assert.Equal("upgraded-hash", credential.PasswordHash);
        Assert.DoesNotContain("secret-password", new LoginCommand("user@identity.example", "secret-password", "native", null).ToString()!);
        Assert.DoesNotContain("secret-access", new TokenPairResult("secret-access", "secret-refresh", 900).ToString()!);
    }
}
