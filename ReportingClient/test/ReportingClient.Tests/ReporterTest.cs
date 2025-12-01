using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ReportingClient.Tests;

public class ReporterTest
{
    private const string Identity = "garden-shed-camera";
    private const string Address = "203.0.113.10";
    private const string Endpoint = "https://registry.example.com/prod";

    public record CapturedRequest(HttpMethod Method, Uri? Uri, string? Body);

    private class StubHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = new();
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync() : null;
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body));
            return _respond(request);
        }
    }

    private static StubHandler JsonHandler(object responseBody) => new(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseBody), Encoding.UTF8, "application/json")
        });

    private static Reporter BuildReporter(HttpMessageHandler handler, Func<Task<string>>? resolveAddress = null) =>
        new(new HttpClient(handler), Endpoint, Identity, resolveAddress ?? (() => Task.FromResult(Address)));

    private static void AssertSubmittedAddress(StubHandler handler, string expected)
    {
        var body = JsonDocument.Parse(handler.Requests.Last().Body!);
        Assert.Equal(expected, body.RootElement.GetProperty("address").GetString());
    }

    [Fact]
    public async Task Report_NewEntry_SubmitsIdentityAndAddressAndReportsCreated()
    {
        var handler = JsonHandler(new { identity = Identity, address = Address, created = true });
        var reporter = BuildReporter(handler);

        var result = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Created, result.Status);
        Assert.Equal(Identity, result.Identity);
        Assert.Equal(Address, result.Address);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{Endpoint}/identity/{Identity}", request.Uri!.ToString());
        AssertSubmittedAddress(handler, Address);
    }

    [Fact]
    public async Task Report_KnownEntry_ReportsUpdated()
    {
        var handler = JsonHandler(new { identity = Identity, address = Address, created = false });
        var reporter = BuildReporter(handler);

        var result = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Updated, result.Status);
        Assert.Equal(Address, result.Address);
    }

    [Fact]
    public async Task Report_UsesAddressFromResolver()
    {
        var handler = JsonHandler(new { created = true });
        var reporter = BuildReporter(handler, () => Task.FromResult("198.51.100.7"));

        var result = await reporter.ReportAsync();

        Assert.Equal("198.51.100.7", result.Address);
        AssertSubmittedAddress(handler, "198.51.100.7");
    }

    [Fact]
    public async Task Report_NonSuccessStatus_ReportsFailureWithStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom", Encoding.UTF8, "text/plain")
        });
        var reporter = BuildReporter(handler);

        var result = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Failed, result.Status);
        Assert.Contains("HTTP 500", result.Detail);
    }

    [Fact]
    public async Task Report_ConnectionError_ReportsFailureWithReason()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var reporter = BuildReporter(handler);

        var result = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Failed, result.Status);
        Assert.Contains("connection refused", result.Detail);
    }

    [Fact]
    public async Task Report_AddressResolutionFailure_ReportsFailureWithoutRequest()
    {
        var handler = JsonHandler(new { created = true });
        var reporter = BuildReporter(handler, () => throw new Exception("no network"));

        var result = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Failed, result.Status);
        Assert.Contains("no network", result.Detail);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Report_AfterFailure_NextCycleSubmitsAgain()
    {
        StubHandler handler = null!;
        handler = new StubHandler(request =>
        {
            if (handler.Requests.Count == 1)
                throw new HttpRequestException("connection refused");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { created = true }), Encoding.UTF8, "application/json")
            };
        });
        var reporter = BuildReporter(handler);

        var first = await reporter.ReportAsync();
        var second = await reporter.ReportAsync();

        Assert.Equal(ReportStatus.Failed, first.Status);
        Assert.Equal(ReportStatus.Created, second.Status);
        Assert.Equal(2, handler.Requests.Count);
        foreach (var request in handler.Requests)
        {
            Assert.Equal($"{Endpoint}/identity/{Identity}", request.Uri!.ToString());
            var body = JsonDocument.Parse(request.Body!);
            Assert.Equal(Address, body.RootElement.GetProperty("address").GetString());
        }
    }
}
