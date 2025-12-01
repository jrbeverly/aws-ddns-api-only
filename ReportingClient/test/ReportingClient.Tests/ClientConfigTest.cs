using Xunit;

namespace ReportingClient.Tests;

public class ClientConfigTest
{
    [Fact]
    public void Config_ReadsEndpointIdentityIntervalAndAddressFromEnvironment()
    {
        SetEnv("REGISTRY_ENDPOINT", "https://registry.example.com/prod");
        SetEnv("IDENTITY", "garden-shed-camera");
        SetEnv("INTERVAL_SECONDS", "30");
        SetEnv("ADDRESS", "203.0.113.10");
        try
        {
            var config = ClientConfig.FromEnvironment();

            Assert.Equal("https://registry.example.com/prod", config.Endpoint);
            Assert.Equal("garden-shed-camera", config.Identity);
            Assert.Equal(30, config.IntervalSeconds);
            Assert.Equal("203.0.113.10", config.Address);
        }
        finally
        {
            ClearEnv("REGISTRY_ENDPOINT", "IDENTITY", "INTERVAL_SECONDS", "ADDRESS");
        }
    }

    [Fact]
    public void Config_DefaultsIntervalTo60WhenUnset()
    {
        SetEnv("REGISTRY_ENDPOINT", "https://registry.example.com/prod");
        SetEnv("IDENTITY", "garden-shed-camera");
        try
        {
            var config = ClientConfig.FromEnvironment();

            Assert.Equal(60, config.IntervalSeconds);
            Assert.Null(config.Address);
        }
        finally
        {
            ClearEnv("REGISTRY_ENDPOINT", "IDENTITY", "INTERVAL_SECONDS", "ADDRESS");
        }
    }

    [Fact]
    public void Config_MissingEndpointOrIdentity_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ClientConfig.FromEnvironment());

        SetEnv("REGISTRY_ENDPOINT", "https://registry.example.com/prod");
        try
        {
            Assert.Throws<InvalidOperationException>(() => ClientConfig.FromEnvironment());
        }
        finally
        {
            ClearEnv("REGISTRY_ENDPOINT", "IDENTITY", "INTERVAL_SECONDS", "ADDRESS");
        }
    }

    private static void SetEnv(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    private static void ClearEnv(params string[] names)
    {
        foreach (var name in names)
            Environment.SetEnvironmentVariable(name, null);
    }
}
