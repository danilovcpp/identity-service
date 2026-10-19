using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Identity.Api.Configuration;

// Invoked in a separate container process, before host/configuration/key loading.
public static class ContainerHealthProbe
{
    public static async Task<int> RunAsync()
    {
        var configuredPort = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
        if (!int.TryParse(configuredPort, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535) { return 1; }
        try
        {
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(1) };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/health/live");
            request.Headers.Host = Environment.GetEnvironmentVariable("IDENTITY_HEALTH_HOST") ?? "localhost";
            using var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK) { return 1; }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("status", out var status) && status.GetString() == "healthy" ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or FormatException or InvalidOperationException)
        {
            return 1; // No connection/configuration values or response contents in health logs.
        }
    }
}
