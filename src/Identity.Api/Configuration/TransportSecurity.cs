using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Identity.Api.Configuration;

public sealed class TransportSecurityOptions
{
    public bool ForwardedHeadersEnabled { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public int ForwardLimit { get; set; } = 1;

    public static bool IsValid(TransportSecurityOptions options) => options.ForwardLimit is >= 1 and <= 5
        && options.KnownProxies.Length <= 16 && (!options.ForwardedHeadersEnabled || options.KnownProxies.Length > 0)
        && options.KnownProxies.All(value => IPAddress.TryParse(value, out var address)
            && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any));
}

public static class TransportSecurity
{
    public static IServiceCollection AddTransportSecurity(this IServiceCollection services)
    {
        services.AddOptions<TransportSecurityOptions>().BindConfiguration("TransportSecurity")
            .Validate(TransportSecurityOptions.IsValid, "Transport security requires explicit trusted proxy addresses and bounded forwarding.")
            .Validate<IConfiguration, IHostEnvironment>((_, config, environment) =>
                !config.GetValue<bool>("FORWARDEDHEADERS_ENABLED") && !config.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
                && (!environment.IsProduction() || (!string.IsNullOrWhiteSpace(config["AllowedHosts"])
                    && !config["AllowedHosts"]!.Contains('*'))), "Production requires explicit hosts and trusted proxy configuration.")
            .ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<TransportSecurityOptions>>((options, settings) =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            // With forwarding disabled no headers are processed; never enable with an empty trust list.
            options.ForwardedHeaders = settings.Value.ForwardedHeadersEnabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto : ForwardedHeaders.None;
            options.ForwardLimit = settings.Value.ForwardLimit;
            options.RequireHeaderSymmetry = true;
            foreach (var value in settings.Value.KnownProxies) { options.KnownProxies.Add(IPAddress.Parse(value)); }
        });
        services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); options.IncludeSubDomains = false; options.Preload = false; });
        return services;
    }
}
