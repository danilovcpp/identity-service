using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Access.GrantAdministrator;
using Identity.Application.Features.Admin.Access.RevokeAdministrator;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class AdminAccessTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Email = "administrator@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    private const string Prefix = "/api/v1/admin/auth";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?> { ["AdminAccess:Enabled"] = "true", ["AdminAccess:DataProtectionKeyPath"] = fixture.AdminKeyDirectory };
        if (overrides is not null) { foreach (var pair in overrides) { settings[pair.Key] = pair.Value; } }
        return fixture.CreateLoginFactory(settings, configure);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<Guid> Seed(WebApplicationFactory<Program> factory, string email = Email, bool grant = true, AdministratorRole role = AdministratorRole.IdentityAdministrator)
    {
        using var scope = factory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
        var user = User.Create(email, "Operator", now);
        user.VerifyEmail(now);
        await using var context = fixture.CreateContext();
        context.AddRange(user, Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), now));
        await context.SaveChangesAsync();
        if (grant) { await Grant(factory, user.Id, role); }
        return user.Id;
    }

    private static async Task Grant(WebApplicationFactory<Program> factory, Guid id, AdministratorRole role = AdministratorRole.IdentityAdministrator)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<GrantAdministratorCommand, Unit>>()
            .Handle(new GrantAdministratorCommand(id, null, role), CancellationToken.None);
    }

    private static async Task Revoke(WebApplicationFactory<Program> factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<RevokeAdministratorCommand, Unit>>()
            .Handle(new RevokeAdministratorCommand(id, null), CancellationToken.None);
    }

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync(Prefix + "/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        return token;
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email = Email, string password = Password)
    {
        await Csrf(client);
        return await client.PostAsJsonAsync(Prefix + "/login", new { email, password });
    }

    [Fact]
    public async Task CliGrantSecureCookieMeLogoutAndReplayWorkAcrossHostRestartWithoutJwtRoleLeak()
    {
        var logs = new SecretLogCapture();
        await using var factory = Factory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var id = await Seed(factory, grant: false);
        Assert.Equal(0, await Cli("grant", "--email", Email));
        using var client = Client(factory);
        using var response = await Login(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminCookieAuthentication.CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        var profile = await client.GetFromJsonAsync<AdminProfile>(Prefix + "/me");
        Assert.Equal(id, profile!.Id);
        Assert.Equal("IdentityAdministrator", profile.Role);
        using (var context = fixture.CreateContext())
        {
            Assert.Empty(await context.Sessions.ToArrayAsync());
            Assert.Empty(await context.RefreshTokens.ToArrayAsync());
            var audit = await context.SecurityAuditEvents.SingleAsync(item => item.EventType == "admin_login");
            Assert.Equal(id, audit.ActorUserId);
            Assert.Equal(id, audit.TargetUserId);
            Assert.Contains(await context.SecurityAuditEvents.ToArrayAsync(), item => item.EventType == "admin_role_granted" && item.ActorUserId == null && item.TargetUserId == id);
        }
        // A new host shares the durable Data Protection ring and the same PostgreSQL session.
        await using var restarted = Factory();
        using var replay = Client(restarted);
        replay.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        using var afterRestart = await replay.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.OK, afterRestart.StatusCode);
        await Csrf(client); // Fetch a new identity-bound token after login.
        using var logout = await client.PostAsync(Prefix + "/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var rejected = await replay.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        await using var verification = fixture.CreateContext();
        Assert.NotNull((await verification.AdminSessions.SingleAsync()).RevokedAt);
        Assert.Contains(await verification.SecurityAuditEvents.ToArrayAsync(), audit => audit.EventType == "admin_logout");
        Assert.DoesNotContain(logs.Lines, line => line.Contains(Password, StringComparison.Ordinal) || line.Contains(Email, StringComparison.Ordinal)
            || line.Contains(cookie.Split(';')[0], StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnonymousAndNonAdministratorsCannotObtainAdministrativeAccess()
    {
        await using var factory = Factory();
        await Seed(factory, grant: false);
        using var client = Client(factory);
        using var anonymous = await client.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        using var forbidden = await Login(client);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.False(forbidden.Headers.TryGetValues("Set-Cookie", out _));
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.AdminSessions.ToArrayAsync());
        using var userLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        var token = (await userLogin.Content.ReadFromJsonAsync<TokenPair>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var bearerOnly = await client.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, bearerOnly.StatusCode);
        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token.AccessToken);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CsrfIsRequiredIncludingLoginAndLogoutAndBoundToBrowserAndIdentity()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = Client(factory);
        using var missing = await client.PostAsJsonAsync(Prefix + "/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("csrf_invalid", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var anonymousToken = await Csrf(client);
        using var other = Client(factory);
        other.DefaultRequestHeaders.Add("X-CSRF-TOKEN", anonymousToken);
        using var crossBrowser = await other.PostAsJsonAsync(Prefix + "/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, crossBrowser.StatusCode);
        using var login = await client.PostAsJsonAsync(Prefix + "/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var staleIdentityToken = await client.PostAsync(Prefix + "/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, staleIdentityToken.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingLogout = await client.PostAsync(Prefix + "/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, missingLogout.StatusCode);
        await Csrf(client);
        using var logout = await client.PostAsync(Prefix + "/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("deactivated")]
    [InlineData("removed-role")]
    [InlineData("missing-credentials")]
    public async Task IneligibleAccountsAndRevokedRolesRejectCookiesAndLogin(string scenario)
    {
        await using var factory = Factory();
        var id = await Seed(factory);
        await Seed(factory, "backup@identity.example");
        using var client = Client(factory);
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        if (scenario == "removed-role") { Assert.Equal(0, await Cli("revoke", "--user-id", id.ToString())); }
        else
        {
            await using var context = fixture.CreateContext();
            var user = await context.Users.SingleAsync(item => item.Id == id);
            if (scenario == "disabled") { user.Disable(DateTimeOffset.UtcNow); }
            if (scenario == "deactivated") { user.Deactivate(DateTimeOffset.UtcNow); }
            if (scenario == "missing-credentials") { context.Credentials.Remove(await context.Credentials.SingleAsync(item => item.UserId == id)); }
            await context.SaveChangesAsync();
        }
        if (scenario != "missing-credentials")
        {
            using var me = await client.GetAsync(Prefix + "/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        using var denied = await Login(client);
        Assert.Equal(scenario == "removed-role" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyIsAuthorizedToReadAndForbiddenToWriteAndCookieCannotAuthenticatePublicApi()
    {
        await using var factory = Factory();
        await Seed(factory, role: AdministratorRole.IdentityReadOnly);
        using var client = Client(factory);
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("IdentityReadOnly", (await client.GetFromJsonAsync<AdminProfile>(Prefix + "/me"))!.Role);
        using var write = await client.PostAsync("/api/v1/admin/test-policy-write", null);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("forbidden", (await write.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var publicApi = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, publicApi.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleAndAbsoluteTimeoutAreEnforcedFromDatabase(bool absolute)
    {
        var clock = new AdminClock(DateTimeOffset.UtcNow);
        await using var factory = Factory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await Seed(factory);
        using var client = Client(factory);
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        if (absolute)
        {
            for (var i = 0; i < 31; i++)
            {
                clock.Now = clock.Now.AddMinutes(15);
                using var active = await client.GetAsync(Prefix + "/me");
                Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            }
            clock.Now = clock.Now.AddMinutes(15);
        }
        else { clock.Now = clock.Now.AddMinutes(30); }
        using var expired = await client.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRoleRevocationsRetainOneActiveAdministratorAndCannotDowngradeIt()
    {
        await using var factory = Factory();
        var first = await Seed(factory);
        var second = await Seed(factory, "second@identity.example");
        var results = await Task.WhenAll(Cli("revoke", "--user-id", first.ToString()), Cli("revoke", "--user-id", second.ToString()));
        Assert.Equal(new[] { 0, 3 }, results.Order().ToArray());
        await using var context = fixture.CreateContext();
        var retained = await context.IdentityAdministrators.SingleAsync();
        await Assert.ThrowsAsync<LastAdministratorException>(() => Grant(factory, retained.UserId, AdministratorRole.IdentityReadOnly));
        Assert.Equal(3, await Cli("revoke", "--user-id", retained.UserId.ToString()));
    }

    [Fact]
    public async Task AdminBruteForceSharesAccountLockoutAndUsesIpAndAccountRateLimits()
    {
        await using var factory = Factory(new() { ["Login:FailureLimit"] = "2" });
        await Seed(factory);
        using var client = Client(factory);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var failed = await Login(client, password: "An incorrect passphrase!");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
        using var locked = await Login(client);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.NotNull((await context.Users.SingleAsync()).LockedUntil);
        Assert.Empty(await context.AdminSessions.ToArrayAsync());

        await using var limitedFactory = Factory(new() { ["AuthRateLimits:AccountPermitLimit"] = "1" });
        using var limited = Client(limitedFactory);
        using var one = await Login(limited);
        using var two = await Login(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, two.StatusCode);
        Assert.NotNull(two.Headers.RetryAfter);
    }

    [Fact]
    public async Task SelfDeactivationCannotRemoveLastAdministratorEvenWhenRacingCliRevocation()
    {
        await using var factory = Factory();
        var first = await Seed(factory);
        var second = await Seed(factory, "second@identity.example");
        using var client = Client(factory);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "second@identity.example", password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        var revoke = Cli("revoke", "--user-id", first.ToString());
        var deactivate = client.PostAsJsonAsync("/api/v1/users/me/deactivate", new { password = Password });
        using var response = await deactivate;
        var exitCode = await revoke;
        Assert.True((exitCode == 0 && response.StatusCode == HttpStatusCode.Conflict)
            || (exitCode == 3 && response.StatusCode == HttpStatusCode.NoContent));
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await (from role in context.IdentityAdministrators
                               join user in context.Users on role.UserId equals user.Id
                               where role.Role == AdministratorRole.IdentityAdministrator && user.Status == UserStatus.Active
                               select user.Id).CountAsync());
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            Assert.Equal("last_administrator", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Equal(UserStatus.Active, (await context.Users.SingleAsync(user => user.Id == second)).Status);
        }
    }

    [Fact]
    public async Task PasswordChangeRevokesAdministrativeAndRefreshSessionsInTheSameTransaction()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var browser = Client(factory);
        using var adminLogin = await Login(browser);
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        using var native = Client(factory);
        using var login = await native.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        native.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        using var change = await native.PostAsJsonAsync("/api/v1/auth/password/change", new { currentPassword = Password, newPassword = "Sailing toward the midnight sun 73!" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        using var me = await browser.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.All(await context.AdminSessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        Assert.All(await context.Sessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task CookieTamperingAndSessionOwnerMismatchAreRejected()
    {
        await using var factory = Factory();
        var first = await Seed(factory);
        var second = await Seed(factory, "second@identity.example");
        using var client = Client(factory);
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var context = fixture.CreateContext();
        var session = await context.AdminSessions.SingleAsync(item => item.UserId == first);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AdminCookieAuthentication.Scheme);
        var forgedBinding = options.TicketDataFormat.Protect(new AuthenticationTicket(
            AdminCookieAuthentication.Principal(second, session.Id, "IdentityAdministrator"),
            new AuthenticationProperties { IssuedUtc = session.CreatedAt, ExpiresUtc = session.ExpiresAt }, AdminCookieAuthentication.Scheme));
        using var mismatch = Client(factory);
        mismatch.DefaultRequestHeaders.Add("Cookie", AdminCookieAuthentication.CookieName + "=" + forgedBinding);
        using var mismatchResponse = await mismatch.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, mismatchResponse.StatusCode);
        using var tampered = Client(factory);
        tampered.DefaultRequestHeaders.Add("Cookie", AdminCookieAuthentication.CookieName + "=invalid-ticket");
        using var tamperedResponse = await tampered.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, tamperedResponse.StatusCode);
    }

    [Fact]
    public async Task RoleChangesAreIdempotentAuditedAndRevokeOldCookies()
    {
        await using var factory = Factory();
        var target = await Seed(factory);
        await Seed(factory, "backup@identity.example");
        using var client = Client(factory);
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Grant(factory, target);
        using var stillActive = await client.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.OK, stillActive.StatusCode);
        await Grant(factory, target, AdministratorRole.IdentityReadOnly);
        using var revoked = await client.GetAsync(Prefix + "/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.SecurityAuditEvents.CountAsync(audit => audit.TargetUserId == target && audit.EventType == "admin_role_granted"));
    }

    [Fact]
    public async Task InvitedUserWithoutCredentialsPersistsButCannotLoginOrReceiveAdminGrant()
    {
        await using var factory = Factory();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var invited = User.CreateInvited(Email, null, now);
        await using (var context = fixture.CreateContext())
        {
            context.Add(invited);
            context.Add(OneTimeToken.Create(invited.Id, OneTimeTokenPurpose.Invitation, new string('b', 64), now, now.AddHours(72)));
            await context.SaveChangesAsync();
            Assert.Empty(await context.Credentials.ToArrayAsync());
        }
        Assert.Equal(4, await Cli("grant", "--user-id", invited.Id.ToString()));
        using var client = Client(factory);
        using var admin = await Login(client);
        Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
        using var user = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.Unauthorized, user.StatusCode);
        using var invalidAcceptance = await client.PostAsJsonAsync("/api/v1/auth/invitations/accept", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAcceptance.StatusCode);
    }

    [Fact]
    public async Task AdminAccessIsOptInAndRequiresPersistentKeysAndHttpsEvenInDevelopment()
    {
        await using var disabled = Factory(new() { ["AdminAccess:Enabled"] = "false" });
        using var disabledClient = Client(disabled);
        using var unavailable = await disabledClient.GetAsync(Prefix + "/csrf");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        await using var missingKeys = Factory(new() { ["AdminAccess:DataProtectionKeyPath"] = "" });
        Assert.Throws<OptionsValidationException>(() => Client(missingKeys));
        await using var enabled = Factory();
        using var http = enabled.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        using var insecure = await http.GetAsync(Prefix + "/csrf");
        Assert.Equal(HttpStatusCode.BadRequest, insecure.StatusCode);
        Assert.Equal("https_required", (await insecure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private async Task<int> Cli(params string[] args)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../../src/Identity.AdminCli/bin/{configuration}/net10.0/Identity.AdminCli.dll"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(path);
        foreach (var arg in args) { start.ArgumentList.Add(arg); }
        using var context = fixture.CreateContext();
        start.Environment["Database__ConnectionString"] = context.Database.GetConnectionString();
        start.Environment["AdminAccess__Enabled"] = "false";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        foreach (var text in await Task.WhenAll(stdout, stderr))
        {
            Assert.DoesNotContain(Password, text);
            Assert.DoesNotContain(Email, text);
            Assert.DoesNotContain(context.Database.GetConnectionString()!, text);
        }
        return process.ExitCode;
    }

    private sealed class AdminClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record TokenPair(string AccessToken, string RefreshToken);
}

// Test-only endpoint exercises the write policy before administrative business commands exist.
[ApiController]
public sealed class AdminWritePolicyProbeController : ControllerBase
{
    [Authorize(Policy = AdminCookieAuthentication.WritePolicy)]
    [HttpPost("/api/v1/admin/test-policy-write")]
    public IActionResult Write() => NoContent();
}
