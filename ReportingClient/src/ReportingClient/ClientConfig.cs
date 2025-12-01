namespace ReportingClient;

public record ClientConfig(string Endpoint, string Identity, int IntervalSeconds, string? Address)
{
    public static ClientConfig FromEnvironment()
    {
        var endpoint = Environment.GetEnvironmentVariable("REGISTRY_ENDPOINT")
            ?? throw new InvalidOperationException("REGISTRY_ENDPOINT is required");
        var identity = Environment.GetEnvironmentVariable("IDENTITY")
            ?? throw new InvalidOperationException("IDENTITY is required");
        var interval = int.TryParse(Environment.GetEnvironmentVariable("INTERVAL_SECONDS"), out var seconds) ? seconds : 60;
        return new ClientConfig(endpoint, identity, interval, Environment.GetEnvironmentVariable("ADDRESS"));
    }
}
