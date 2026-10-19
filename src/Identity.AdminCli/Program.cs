using Identity.Application;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Access.GrantAdministrator;
using Identity.Application.Features.Admin.Access.RevokeAdministrator;
using Identity.Domain.Enums;
using Identity.Domain.Exceptions;
using Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args is ["--help"])
{
    Console.WriteLine("Identity.AdminCli grant|revoke --user-id <UUID>|--email <exact-email> [--role IdentityAdministrator|IdentityReadOnly]");
    Console.WriteLine("Database__ConnectionString must be supplied through a protected environment. No HTTP service is started.");
    return 0;
}

if (args.Length is not (3 or 5) || args[0] is not ("grant" or "revoke") || args[1] is not ("--user-id" or "--email")
    || (args.Length == 5 && (args[0] != "grant" || args[3] != "--role" || args[4] is not ("IdentityAdministrator" or "IdentityReadOnly"))))
{
    Console.Error.WriteLine("Invalid arguments. Use --help. Secrets are never accepted as command-line arguments.");
    return 2;
}
Guid? userId = null;
if (args[1] == "--user-id")
{
    if (!Guid.TryParse(args[2], out var parsed) || parsed == Guid.Empty) { Console.Error.WriteLine("Invalid user identifier."); return 2; }
    userId = parsed;
}
var email = userId is null ? args[2] : null;
var role = args.Length == 5 && args[4] == "IdentityReadOnly" ? AdministratorRole.IdentityReadOnly : AdministratorRole.IdentityAdministrator;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], DisableDefaults = true });
    builder.Configuration.AddEnvironmentVariables();
    builder.Logging.ClearProviders(); // No raw database/configuration/exception messages in CLI output.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();
    using var host = builder.Build();
    // Do not start hosted API workers or load JWT/SMTP keys. This is a local transactional DB tool.
    using var scope = host.Services.CreateScope();
    if (args[0] == "grant")
    {
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<GrantAdministratorCommand, Unit>>()
            .Handle(new GrantAdministratorCommand(userId, email, role), cancellation.Token);
    }
    else
    {
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<RevokeAdministratorCommand, Unit>>()
            .Handle(new RevokeAdministratorCommand(userId, email), cancellation.Token);
    }
    Console.WriteLine("Administrative access updated. The operation is idempotent; changes are audited.");
    return 0;
}
catch (LastAdministratorException) { Console.Error.WriteLine("Refused: the last active IdentityAdministrator must be retained."); return 3; }
catch (AdminTargetIneligibleException) { Console.Error.WriteLine("Refused: target is absent or is not eligible for this operation."); return 4; }
catch (DomainException) { Console.Error.WriteLine("Refused: invalid input or account state."); return 4; }
catch (OperationCanceledException) { Console.Error.WriteLine("Operation cancelled."); return 5; }
catch (Exception) { Console.Error.WriteLine("Operation failed. Check protected database configuration, connectivity and applied migrations."); return 1; }
