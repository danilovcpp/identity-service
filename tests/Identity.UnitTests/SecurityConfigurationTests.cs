using Identity.Api.Configuration;
using Identity.Infrastructure.Maintenance;

namespace Identity.UnitTests;

public sealed class SecurityConfigurationTests
{
    [Theory]
    [InlineData(false, "", true)]
    [InlineData(true, "", false)]
    [InlineData(true, "127.0.0.1", true)]
    [InlineData(true, "::1", true)]
    [InlineData(true, "0.0.0.0", false)]
    [InlineData(true, "::", false)]
    [InlineData(true, "192.0.2.0/24", false)]
    [InlineData(true, "*", false)]
    public void ForwardingRequiresExplicitIpAddresses(bool enabled, string address, bool valid) =>
        Assert.Equal(valid, TransportSecurityOptions.IsValid(new TransportSecurityOptions
        { ForwardedHeadersEnabled = enabled, KnownProxies = address.Length == 0 ? [] : [address] }));

    [Fact]
    public void MaintenanceDefaultsKeepNinetyDaysOfAuditAndRejectUnboundedBatches()
    {
        var options = new SecurityMaintenanceOptions();
        Assert.Equal(90, options.AuditRetentionDays);
        Assert.True(SecurityMaintenanceOptions.IsValid(options));
        options.BatchSize = 0;
        Assert.False(SecurityMaintenanceOptions.IsValid(options));
        options.BatchSize = 1001;
        Assert.False(SecurityMaintenanceOptions.IsValid(options));
    }
}
