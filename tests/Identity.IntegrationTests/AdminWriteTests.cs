using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Users.DisableUser;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class AdminWriteTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Email = "writer@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    private const string NewPassword = "Sailing toward the midnight sun 73!";
    private const string Prefix = "/api/v1/admin/users";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AdminAccess:Enabled"] = "true",
            ["AdminAccess:DataProtectionKeyPath"] = fixture.AdminKeyDirectory,
            ["Invitations:Enabled"] = "true",
            ["Email:InvitationUrlTemplate"] = "http://localhost:3000/account/activate#token={token}",
            ["PasswordReset:Enabled"] = "true",
            ["Email:ResetPasswordUrlTemplate"] = "http://localhost:3000/account/reset-password#token={token}"
        };
        if (overrides is not null) { foreach (var pair in overrides) { settings[pair.Key] = pair.Value; } }
        return fixture.CreateLoginFactory(settings, configure);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<Guid> Seed(WebApplicationFactory<Program> factory, string email = Email,
        AdministratorRole? role = AdministratorRole.IdentityAdministrator, UserStatus status = UserStatus.Active)
    {
        using var scope = factory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
        var user = status == UserStatus.Invited ? User.CreateInvited(email, "Original", now) : User.Create(email, "Original", now);
        if (status is UserStatus.Active or UserStatus.Disabled or UserStatus.Deactivated) { user.VerifyEmail(now); }
        if (status == UserStatus.Disabled) { user.Disable(now); }
        if (status == UserStatus.Deactivated) { user.Deactivate(now); }
        await using var context = fixture.CreateContext();
        context.Add(user);
        if (status != UserStatus.Invited) { context.Add(Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), now)); }
        if (role is { } assigned) { context.Add(IdentityAdministrator.Grant(user, assigned, null, now)); }
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static async Task Csrf(HttpClient client)
    {
        using var csrf = await client.GetAsync("/api/v1/admin/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }

    private static async Task LoginAdmin(HttpClient client, string email = Email)
    {
        await Csrf(client);
        using var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Csrf(client);
    }

    private static Task<HttpResponseMessage> Accept(HttpClient client, string token, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/invitations/accept", new { token, newPassword = password });

    private async Task<Guid> Invite(HttpClient client, string email = "invited@identity.example")
    {
        using var invite = await client.PostAsJsonAsync(Prefix + "/invitations", new { email, displayName = "Invited", password = "Ignored field" });
        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        Assert.Empty(await invite.Content.ReadAsStringAsync());
        await using var context = fixture.CreateContext();
        return (await context.Users.SingleAsync(user => user.Email == email)).Id;
    }

    private static async Task<TokenPair> LoginUser(HttpClient client, string email, string password = Password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, clientId = "native" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
    }

    private static async Task<HttpResponseMessage> Protected(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task InvitationMailpitAcceptanceAndLoginAreAtomicSingleUseAndNeverExposeSecretsToAdmin()
    {
        var logs = new SecretLogCapture();
        await using var factory = Factory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var actor = await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var id = await Invite(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        await using (var context = fixture.CreateContext())
        {
            Assert.Equal(UserStatus.Invited, (await context.Users.SingleAsync(user => user.Id == id)).Status);
            Assert.False(await context.Credentials.AnyAsync(credential => credential.UserId == id));
            var stored = await context.OneTimeTokens.SingleAsync();
            Assert.Equal(TimeSpan.FromHours(72), stored.ExpiresAt - stored.CreatedAt);
            Assert.Equal(OneTimeTokenPurpose.Invitation, stored.Purpose);
            Assert.Equal(factory.Services.GetRequiredService<ITokenGenerator>().Hash(token), stored.TokenHash);
            Assert.DoesNotContain(token, (await context.Database.SqlQueryRaw<string>("SELECT payload::text AS \"Value\" FROM outbox_messages").SingleAsync()));
            var audit = await context.SecurityAuditEvents.SingleAsync(audit => audit.EventType == "admin_invited_user");
            Assert.Equal(actor, audit.ActorUserId);
            Assert.Equal(id, audit.TargetUserId);
        }
        Assert.True(await fixture.ProcessAsync(factory));
        Assert.Equal(1, await fixture.DeliveredCountAsync());
        Assert.True(token == await fixture.DeliveredTokenAsync());
        using var publicClient = Client(factory);
        using var accepted = await Accept(publicClient, token);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using var repeated = await Accept(publicClient, token);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        var pair = await LoginUser(publicClient, "invited@identity.example");
        using var me = await Protected(publicClient, pair.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        await using var after = fixture.CreateContext();
        var user = await after.Users.SingleAsync(user => user.Id == id);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.True(await after.Credentials.AnyAsync(credential => credential.UserId == id));
        Assert.False(await after.IdentityAdministrators.AnyAsync(role => role.UserId == id));
        Assert.NotNull((await after.OneTimeTokens.SingleAsync()).ConsumedAt);
        Assert.Null((await after.OutboxMessages.SingleAsync()).Payload);
        Assert.DoesNotContain(logs.Lines, line => new[] { Password, token, pair.AccessToken, pair.RefreshToken, "invited@identity.example" }
            .Any(secret => line.Contains(secret, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("resend")]
    [InlineData("expiry")]
    [InlineData("disable")]
    public async Task InvalidatedInvitationCannotActivateAndErrorsAreGeneric(string scenario)
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = Factory(new() { ["Invitations:LifetimeSeconds"] = "60" },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var id = await Invite(client);
        var old = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        if (scenario == "resend")
        {
            using var response = await client.PostAsync(Prefix + $"/{id}/invitations/resend", null);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        else if (scenario == "expiry") { clock.Advance(TimeSpan.FromSeconds(60)); }
        else
        {
            using var disabled = await client.PostAsJsonAsync(Prefix + $"/{id}/disable", new { reason = "Operator decision" });
            Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
            using var enabled = await client.PostAsync(Prefix + $"/{id}/enable", null);
            Assert.Equal(HttpStatusCode.NoContent, enabled.StatusCode);
        }
        using var invalid = await Accept(client, old);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid_invitation_token", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using (var context = fixture.CreateContext())
        {
            Assert.Equal(UserStatus.Invited, (await context.Users.SingleAsync(user => user.Id == id)).Status);
            Assert.False(await context.Credentials.AnyAsync(credential => credential.UserId == id));
        }
        if (scenario == "resend")
        {
            var next = (await fixture.TokensFromOutboxAsync(factory)).Single(token => token != old);
            using var accepted = await Accept(client, next);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        }
    }

    [Fact]
    public async Task DuplicateInvitationRaceReturnsOneAcceptedAndOneConflictAndRegistrationCannotOverwriteInvite()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(Prefix + "/invitations", new { email = "dupe@identity.example" })));
        try { Assert.Equal(new[] { HttpStatusCode.Accepted, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).Order().ToArray()); }
        finally { foreach (var response in responses) { response.Dispose(); } }
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = " DUPE@identity.example ", password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(user => user.Email == "dupe@identity.example");
        Assert.Equal(UserStatus.Invited, user.Status);
        Assert.False(await context.Credentials.AnyAsync(credential => credential.UserId == user.Id));
        Assert.Single(await context.OutboxMessages.ToArrayAsync());
        Assert.Single(await context.OneTimeTokens.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentAcceptancesCreateExactlyOneCredentialAndOneActivationEvent()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var id = await Invite(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        var responses = await Task.WhenAll(Accept(client, token), Accept(client, token));
        try { Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest }, responses.Select(response => response.StatusCode).Order().ToArray()); }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Credentials.CountAsync(credential => credential.UserId == id));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "invitation_accepted"));
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Invited)]
    public async Task DisableEnablePreservesStateMetadataAndNeverReactivatesSessions(UserStatus initial)
    {
        await using var factory = Factory();
        var actor = await Seed(factory);
        var target = await Seed(factory, "target@identity.example", null, initial);
        using var client = Client(factory);
        await LoginAdmin(client);
        TokenPair? pair = initial == UserStatus.Active ? await LoginUser(client, "target@identity.example") : null;
        using var disabled = await client.PostAsJsonAsync(Prefix + $"/{target}/disable", new { reason = "  Security review  " });
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        using var repeated = await client.PostAsJsonAsync(Prefix + $"/{target}/disable", new { reason = "Different reason" });
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync(user => user.Id == target);
            Assert.Equal(UserStatus.Disabled, user.Status);
            Assert.Equal(initial, user.StatusBeforeDisable);
            Assert.Equal(actor, user.DisabledBy);
            Assert.Equal("Security review", user.DisableReason);
            Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "admin_disabled_user"));
        }
        if (pair is not null)
        {
            using var protectedResponse = await Protected(client, pair.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
            using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var loginBlocked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "target@identity.example", password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginBlocked.StatusCode);
        using var enabled = await client.PostAsync(Prefix + $"/{target}/enable", null);
        Assert.Equal(HttpStatusCode.NoContent, enabled.StatusCode);
        await using var after = fixture.CreateContext();
        var restored = await after.Users.SingleAsync(user => user.Id == target);
        Assert.Equal(initial, restored.Status);
        Assert.Equal(initial == UserStatus.Active, restored.EmailVerifiedAt is not null);
        Assert.Null(restored.StatusBeforeDisable);
        Assert.Null(restored.DisabledBy);
        Assert.All(await after.Sessions.Where(session => session.UserId == target).ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task DisablingAdminRevokesCookieAndRefreshAndLastAdministratorCannotBeDisabled()
    {
        await using var factory = Factory();
        var actor = await Seed(factory);
        var target = await Seed(factory, "otheradmin@identity.example");
        using var client = Client(factory);
        using var other = Client(factory);
        await LoginAdmin(client);
        await LoginAdmin(other, "otheradmin@identity.example");
        var pair = await LoginUser(other, "otheradmin@identity.example");
        using var disabled = await client.PostAsJsonAsync(Prefix + $"/{target}/disable", new { reason = "Compromised account" });
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        using var me = await other.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var refresh = await other.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var last = await client.PostAsJsonAsync(Prefix + $"/{actor}/disable", new { reason = "Refuse last admin" });
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
        Assert.Equal("last_administrator", (await last.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var context = fixture.CreateContext();
        Assert.All(await context.AdminSessions.Where(session => session.UserId == target).ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        Assert.Equal(UserStatus.Active, (await context.Users.SingleAsync(user => user.Id == actor)).Status);
    }

    [Fact]
    public async Task ConcurrentSelfDisablesRetainOneActiveFullAdministrator()
    {
        await using var factory = Factory();
        var first = await Seed(factory);
        var second = await Seed(factory, "second@identity.example");
        using var a = Client(factory);
        using var b = Client(factory);
        await LoginAdmin(a);
        await LoginAdmin(b, "second@identity.example");
        var responses = await Task.WhenAll(a.PostAsJsonAsync(Prefix + $"/{first}/disable", new { reason = "First" }),
            b.PostAsJsonAsync(Prefix + $"/{second}/disable", new { reason = "Second" }));
        try { Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).Order().ToArray()); }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync(user => user.Status == UserStatus.Active));
    }

    [Fact]
    public async Task AdminResetUsesMailpitAndRevokesAllNativeAndAdminSessionsOnlyUponAcceptance()
    {
        await using var factory = Factory();
        await Seed(factory);
        var target = await Seed(factory, "resetadmin@identity.example");
        using var client = Client(factory);
        using var targetBrowser = Client(factory);
        await LoginAdmin(client);
        await LoginAdmin(targetBrowser, "resetadmin@identity.example");
        var pair = await LoginUser(targetBrowser, "resetadmin@identity.example");
        using var request = await client.PostAsync(Prefix + $"/{target}/password-reset", null);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        Assert.Empty(await request.Content.ReadAsStringAsync());
        using var before = await targetBrowser.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.True(await fixture.ProcessAsync(factory));
        var token = await fixture.DeliveredTokenAsync();
        using var reset = await targetBrowser.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var repeated = await targetBrowser.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        using var after = await targetBrowser.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        using var refresh = await targetBrowser.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        await LoginUser(targetBrowser, "resetadmin@identity.example", NewPassword);
    }

    [Fact]
    public async Task TargetedAndBulkRevocationRespectOwnershipAndInvalidateBothSessionTransports()
    {
        await using var factory = Factory();
        var actor = await Seed(factory);
        var target = await Seed(factory, "targetadmin@identity.example");
        using var client = Client(factory);
        using var targetBrowser = Client(factory);
        await LoginAdmin(client);
        await LoginAdmin(targetBrowser, "targetadmin@identity.example");
        var pair = await LoginUser(targetBrowser, "targetadmin@identity.example");
        Guid session;
        await using (var context = fixture.CreateContext()) { session = (await context.Sessions.SingleAsync()).Id; }
        using var foreign = await client.DeleteAsync(Prefix + $"/{actor}/sessions/{session}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var revoke = await client.DeleteAsync(Prefix + $"/{target}/sessions/{session}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var repeated = await client.DeleteAsync(Prefix + $"/{target}/sessions/{session}");
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        using var failed = await targetBrowser.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        using var stillAdmin = await targetBrowser.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, stillAdmin.StatusCode);
        var next = await LoginUser(targetBrowser, "targetadmin@identity.example");
        using var all = await client.PostAsync(Prefix + $"/{target}/sessions/revoke-all", null);
        Assert.Equal(HttpStatusCode.NoContent, all.StatusCode);
        using var cookieDenied = await targetBrowser.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, cookieDenied.StatusCode);
        using var nativeDenied = await Protected(targetBrowser, next.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, nativeDenied.StatusCode);
    }

    [Theory]
    [InlineData("invite")]
    [InlineData("resend")]
    [InlineData("reset")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("revoke")]
    [InlineData("revoke-all")]
    public async Task AllAdministrativeWritesDenyReadOnlyAndRequireCsrf(string action)
    {
        await using var factory = Factory();
        await Seed(factory);
        await Seed(factory, "reader@identity.example", AdministratorRole.IdentityReadOnly);
        var target = await Seed(factory, "target@identity.example", null);
        using var reader = Client(factory);
        await LoginAdmin(reader, "reader@identity.example");
        using var denied = await Action(reader, action, target, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var admin = Client(factory);
        await LoginAdmin(admin);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf = await Action(admin, action, target, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal("csrf_invalid", (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("invite")]
    [InlineData("resend")]
    [InlineData("reset")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("revoke")]
    [InlineData("revoke-all")]
    [InlineData("accept")]
    public async Task PreCommitFailureRollsBackUserTokensSessionsOutboxAndAudit(string action)
    {
        var failure = new WriteFailure();
        await using var factory = Factory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var target = action is "resend" or "accept" ? await Invite(client) : await Seed(factory, "target@identity.example", null,
            action == "enable" ? UserStatus.Disabled : UserStatus.Active);
        var token = action == "accept" ? Assert.Single(await fixture.TokensFromOutboxAsync(factory)) : "";
        Guid session;
        await using (var context = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            var family = Session.Create(target, "native", null, now, now.AddDays(1), "identity-native");
            context.AddRange(family, RefreshToken.Create(family, new string('a', 64), now, now.AddHours(1)));
            await context.SaveChangesAsync();
            session = family.Id;
        }
        string before;
        await using (var context = fixture.CreateContext()) { before = await Snapshot(context); }
        failure.Enabled = true;
        using var response = action == "accept" ? await Accept(client, token) : await Action(client, action, target, session);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var after = fixture.CreateContext();
        Assert.Equal(before, await Snapshot(after));
    }

    private static Task<HttpResponseMessage> Action(HttpClient client, string action, Guid target, Guid session) => action switch
    {
        "invite" => client.PostAsJsonAsync(Prefix + "/invitations", new { email = "new@identity.example" }),
        "resend" => client.PostAsync(Prefix + $"/{target}/invitations/resend", null),
        "reset" => client.PostAsync(Prefix + $"/{target}/password-reset", null),
        "disable" => client.PostAsJsonAsync(Prefix + $"/{target}/disable", new { reason = "Review" }),
        "enable" => client.PostAsync(Prefix + $"/{target}/enable", null),
        "revoke" => client.DeleteAsync(Prefix + $"/{target}/sessions/{session}"),
        _ => client.PostAsync(Prefix + $"/{target}/sessions/revoke-all", null)
    };

    [Theory]
    [InlineData("readonly")]
    [InlineData("no-role")]
    [InlineData("revoked")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task HandlerRechecksActorAuthorityAndSessionInsideMutationTransaction(string scenario)
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = Factory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        var actor = await Seed(factory);
        var target = await Seed(factory, "target@identity.example", null);
        using var client = Client(factory);
        await LoginAdmin(client);
        Guid sessionId;
        await using (var context = fixture.CreateContext())
        {
            var session = await context.AdminSessions.SingleAsync();
            sessionId = session.Id;
            if (scenario == "revoked") { session.Revoke(clock.GetUtcNow()); }
            if (scenario == "disabled") { (await context.Users.SingleAsync(user => user.Id == actor)).Disable(clock.GetUtcNow()); }
            if (scenario is "readonly" or "no-role")
            { context.IdentityAdministrators.Remove(await context.IdentityAdministrators.SingleAsync(role => role.UserId == actor)); }
            await context.SaveChangesAsync();
            if (scenario == "readonly")
            {
                context.IdentityAdministrators.Add(IdentityAdministrator.Grant(await context.Users.SingleAsync(user => user.Id == actor),
                    AdministratorRole.IdentityReadOnly, null, clock.GetUtcNow()));
                await context.SaveChangesAsync();
            }
        }
        if (scenario == "expired") { clock.Advance(TimeSpan.FromMinutes(30)); }
        using var scope = factory.Services.CreateScope();
        var command = new DisableUserCommand(actor, sessionId, target, "Must not run");
        var execute = () => scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Execute<DisableUserCommand, Unit>(command);
        if (scenario is "readonly" or "no-role") { await Assert.ThrowsAsync<AdminForbiddenException>(execute); }
        else { await Assert.ThrowsAsync<UnauthorizedSessionException>(execute); }
        await using var after = fixture.CreateContext();
        Assert.Equal(UserStatus.Active, (await after.Users.SingleAsync(user => user.Id == target)).Status);
        Assert.False(await after.SecurityAuditEvents.AnyAsync(audit => audit.EventType == "admin_disabled_user"));
    }

    [Theory]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Invited)]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Deactivated)]
    public async Task ResetRequiresActiveVerifiedUserAndDeactivatedAccountCannotBeEnabled(UserStatus status)
    {
        await using var factory = Factory();
        await Seed(factory);
        var target = await Seed(factory, "target@identity.example", null, status);
        using var client = Client(factory);
        await LoginAdmin(client);
        using var reset = await client.PostAsync(Prefix + $"/{target}/password-reset", null);
        Assert.Equal(HttpStatusCode.Conflict, reset.StatusCode);
        if (status == UserStatus.Deactivated)
        {
            using var enabled = await client.PostAsync(Prefix + $"/{target}/enable", null);
            Assert.Equal(HttpStatusCode.Conflict, enabled.StatusCode);
        }
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.OutboxMessages.ToArrayAsync());
        Assert.Empty(await context.OneTimeTokens.ToArrayAsync());
    }

    [Fact]
    public async Task InvitationsArePurposeBoundAndExistingCredentialsCannotBeOverwritten()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var id = await Invite(client);
        var invitation = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        var unrelated = factory.Services.GetRequiredService<ITokenGenerator>().Generate();
        await using (var context = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            context.Add(OneTimeToken.Create(id, OneTimeTokenPurpose.PasswordReset,
                factory.Services.GetRequiredService<ITokenGenerator>().Hash(unrelated), now, now.AddMinutes(30)));
            await context.SaveChangesAsync();
        }
        using var wrongPurpose = await Accept(client, unrelated);
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        await using (var context = fixture.CreateContext())
        { context.Add(Credential.Create(id, "credential-sentinel", DateTimeOffset.UtcNow)); await context.SaveChangesAsync(); }
        using var conflict = await Accept(client, invitation);
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        await using var after = fixture.CreateContext();
        Assert.Equal(UserStatus.Invited, (await after.Users.SingleAsync(user => user.Id == id)).Status);
        Assert.True((await after.Credentials.SingleAsync(credential => credential.UserId == id)).PasswordHash == "credential-sentinel");
    }

    [Theory]
    [InlineData("invite-off")]
    [InlineData("ttl")]
    [InlineData("query-invitation")]
    [InlineData("query-reset")]
    public async Task FeatureAndUrlConfigurationAreValidated(string scenario)
    {
        var overrides = scenario switch
        {
            "invite-off" => new Dictionary<string, string?> { ["Invitations:Enabled"] = "false" },
            "ttl" => new() { ["Invitations:LifetimeSeconds"] = "259201" },
            "query-invitation" => new() { ["Email:InvitationUrlTemplate"] = "http://localhost/activate?token={token}" },
            _ => new() { ["Email:ResetPasswordUrlTemplate"] = "http://localhost/reset?token={token}" }
        };
        await using var factory = Factory(overrides);
        if (scenario != "invite-off")
        {
            var error = Record.Exception(() => Client(factory));
            Assert.NotNull(error);
            var errors = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [error];
            Assert.All(errors, exception => Assert.IsType<OptionsValidationException>(exception));
            return;
        }
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        using var unavailable = await client.PostAsJsonAsync(Prefix + "/invitations", new { email = "new@identity.example" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using var rejected = await Accept(client, new string('a', 43));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    private static async Task<string> Snapshot(IdentityDbContext context)
    {
        var snapshot = await context.Database.SqlQueryRaw<string>("""
        SELECT jsonb_build_object(
          'users', (SELECT jsonb_agg(to_jsonb(u) ORDER BY id) FROM users u),
          'credentials', (SELECT jsonb_agg(to_jsonb(c) ORDER BY user_id) FROM credentials c),
          'sessions', (SELECT jsonb_agg(to_jsonb(s) ORDER BY id) FROM sessions s),
          'refresh', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM refresh_tokens r),
          'adminSessions', (SELECT jsonb_agg(to_jsonb(s) - 'last_seen_at' ORDER BY id) FROM admin_sessions s),
          'tokens', (SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM one_time_tokens t),
          'outbox', (SELECT jsonb_agg(to_jsonb(o) ORDER BY id) FROM outbox_messages o),
          'audit', (SELECT jsonb_agg(to_jsonb(a) ORDER BY id) FROM security_audit_events a))::text AS "Value"
        """).SingleAsync();
        // Assertion output must never contain stored hashes or encrypted messages on failure.
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(snapshot)));
    }

    [Fact]
    public async Task RegistrationWinningBetweenInviteLookupAndSaveReturnsAdmin409WithoutPartialRecords()
    {
        HttpClient? native = null;
        var race = new BeforeInvitationSave(async () =>
        {
            using var registered = await native!.PostAsJsonAsync("/api/v1/auth/register", new { email = "race@identity.example", password = Password, clientId = "native" });
            Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        });
        await using var factory = Factory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(race)));
        await Seed(factory);
        using var admin = Client(factory);
        using var publicClient = Client(factory);
        native = publicClient;
        await LoginAdmin(admin);
        using var response = await admin.PostAsJsonAsync(Prefix + "/invitations", new { email = "race@identity.example" });
        Assert.True(race.Called);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("admin_email_conflict", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var context = fixture.CreateContext();
        var target = await context.Users.SingleAsync(user => user.Email == "race@identity.example");
        Assert.Equal(UserStatus.PendingVerification, target.Status);
        Assert.True(await context.Credentials.AnyAsync(credential => credential.UserId == target.Id));
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
        Assert.Equal(1, await context.OneTimeTokens.CountAsync());
        Assert.False(await context.SecurityAuditEvents.AnyAsync(audit => audit.EventType == "admin_invited_user"));
    }

    [Fact]
    public async Task ResendRacingAcceptanceSerializesTokenAndStatusTransitions()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        var target = await Invite(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        var accept = Accept(client, token);
        var resend = client.PostAsync(Prefix + $"/{target}/invitations/resend", null);
        using var accepted = await accept;
        using var resent = await resend;
        Assert.True((accepted.StatusCode == HttpStatusCode.NoContent && resent.StatusCode == HttpStatusCode.Conflict)
            || (accepted.StatusCode == HttpStatusCode.BadRequest && resent.StatusCode == HttpStatusCode.Accepted));
        await using var context = fixture.CreateContext();
        Assert.Equal(accepted.StatusCode == HttpStatusCode.NoContent ? 1 : 0, await context.Credentials.CountAsync(credential => credential.UserId == target));
    }

    [Fact]
    public async Task DisableRacingRefreshNeverLeavesAnActiveReplacement()
    {
        await using var factory = Factory();
        await Seed(factory);
        var target = await Seed(factory, "target@identity.example", null);
        using var client = Client(factory);
        await LoginAdmin(client);
        var pair = await LoginUser(client, "target@identity.example");
        var disable = client.PostAsJsonAsync(Prefix + $"/{target}/disable", new { reason = "Security incident" });
        var refresh = client.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });
        using var disabled = await disable;
        using var rotated = await refresh;
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.True(rotated.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
        if (rotated.StatusCode == HttpStatusCode.OK)
        {
            var replacement = (await rotated.Content.ReadFromJsonAsync<TokenPair>())!;
            using var denied = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { replacement.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        await using var context = fixture.CreateContext();
        Assert.All(await context.Sessions.ToArrayAsync(), session => Assert.NotNull(session.RevokedAt));
        Assert.All(await context.RefreshTokens.ToArrayAsync(), token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task InvitationsAloneLoadOutboxKeysAndSmtpFailureKeepsEncryptedPayload()
    {
        await using var factory = Factory(new()
        {
            ["Registration:Enabled"] = "false",
            ["PasswordReset:Enabled"] = "false",
            ["Jwt:Enabled"] = "false",
            ["Email:VerificationUrlTemplate"] = "",
            ["Email:ResetPasswordUrlTemplate"] = "",
            ["Email:Smtp:Port"] = "1"
        });
        await Seed(factory);
        using var client = Client(factory);
        await LoginAdmin(client);
        await Invite(client);
        Assert.True(await fixture.ProcessAsync(factory));
        await using var context = fixture.CreateContext();
        var message = await context.OutboxMessages.SingleAsync();
        Assert.Null(message.ProcessedAt);
        Assert.NotNull(message.Payload);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("smtp_unavailable", message.LastError);
        Assert.Single(await fixture.TokensFromOutboxAsync(factory));
    }

    private sealed record TokenPair(string AccessToken, string RefreshToken);
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed class WriteFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            Enabled && eventData.Context!.ChangeTracker.Entries<SecurityAuditEvent>().Any()
                ? throw new InvalidOperationException("Injected before commit.") : ValueTask.FromResult(result);
    }

    private sealed class BeforeInvitationSave(Func<Task> action) : SaveChangesInterceptor
    {
        public bool Called { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Called && eventData.Context!.ChangeTracker.Entries<User>().Any(entry => entry.State == EntityState.Added && entry.Entity.Status == UserStatus.Invited))
            { Called = true; await action(); }
            return result;
        }
    }
}
