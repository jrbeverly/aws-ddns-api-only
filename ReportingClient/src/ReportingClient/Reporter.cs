using System.Net;
using System.Text;
using System.Text.Json;

namespace ReportingClient;

public enum ReportStatus { Created, Updated, Failed }

public record ReportResult(ReportStatus Status, string Identity, string Address, string Detail);

public class Reporter
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _identity;
    private readonly Func<Task<string>> _resolveAddress;

    public Reporter(HttpClient http, string endpoint, string identity, Func<Task<string>> resolveAddress)
    {
        _http = http;
        _endpoint = endpoint.TrimEnd('/');
        _identity = identity;
        _resolveAddress = resolveAddress;
    }

    public async Task<ReportResult> ReportAsync()
    {
        string address;
        try
        {
            address = (await _resolveAddress()).Trim();
        }
        catch (Exception e)
        {
            return new ReportResult(ReportStatus.Failed, _identity, "", $"address resolution failed: {e.Message}");
        }

        try
        {
            using var response = await _http.PostAsync(
                $"{_endpoint}/identity/{Uri.EscapeDataString(_identity)}",
                new StringContent(JsonSerializer.Serialize(new { address }), Encoding.UTF8, "application/json"));

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return new ReportResult(ReportStatus.Failed, _identity, address, $"HTTP {(int)response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var created = doc.RootElement.TryGetProperty("created", out var createdElement) && createdElement.GetBoolean();
            return new ReportResult(created ? ReportStatus.Created : ReportStatus.Updated, _identity, address, "");
        }
        catch (Exception e)
        {
            return new ReportResult(ReportStatus.Failed, _identity, address, e.Message);
        }
    }
}
