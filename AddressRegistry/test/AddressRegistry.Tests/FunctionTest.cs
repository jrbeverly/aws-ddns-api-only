using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.DynamoDBv2.DocumentModel;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace AddressRegistry.Tests;

public class FunctionTest
{
    private const string Identity = "garden-shed-camera";
    private const string Address = "203.0.113.10";
    private const string LastReported = "2026-09-29T12:00:00Z";
    private const string LastChanged = "2026-09-28T08:15:00Z";

    private static readonly Func<DateTime> Clock =
        () => new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc);
    private const string Now = "2026-09-30T06:00:00.0000000Z";

    private static APIGatewayProxyRequest GetRequest(string identity) =>
        new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/identity/{identity}",
            PathParameters = new Dictionary<string, string> { { "proxy", $"identity/{identity}" } }
        };

    private static APIGatewayProxyRequest PostRequest(string identity, string body) =>
        new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = $"/identity/{identity}",
            PathParameters = new Dictionary<string, string> { { "proxy", $"identity/{identity}" } },
            Body = body
        };

    private static APIGatewayProxyRequest PostAddress(string identity, string address) =>
        PostRequest(identity, JsonConvert.SerializeObject(new { address }));

    private static Document Entry(string identity, string address) => new Document
    {
        ["identity"] = identity,
        ["address"] = address,
        ["last_reported"] = LastReported,
        ["last_changed"] = LastChanged
    };

    private (Function function, Mock<ITable> tableMock) BuildFunction(Document? stored)
    {
        var tableMock = new Mock<ITable>();
        tableMock
            .Setup(t => t.GetItemAsync(It.IsAny<Primitive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored!);
        return (new Function(tableMock.Object, Clock), tableMock);
    }

    [Fact]
    public void Get_KnownIdentity_ReturnsAddressWithFreshnessTimestamps()
    {
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));

        var response = function.FunctionHandler(GetRequest(Identity), new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        var body = JsonConvert.DeserializeAnonymousType(response.Body,
            new { identity = "", address = "", last_reported = "", last_changed = "" });
        Assert.Equal(Identity, body!.identity);
        Assert.Equal(Address, body.address);
        Assert.Equal(LastReported, body.last_reported);
        Assert.Equal(LastChanged, body.last_changed);
        tableMock.Verify(t => t.GetItemAsync(
            It.Is<Primitive>(p => p.Value.Equals(Identity)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Get_UnknownIdentity_ReturnsNotFoundWithoutAddressField()
    {
        var (function, _) = BuildFunction(null);

        var response = function.FunctionHandler(GetRequest("missing-camera"), new TestLambdaContext());

        Assert.Equal(404, response.StatusCode);
        var body = JsonConvert.DeserializeAnonymousType(response.Body, new { identity = "", found = false });
        Assert.Equal("missing-camera", body!.identity);
        Assert.False(body.found);
        Assert.DoesNotContain("address", response.Body);
    }

    [Fact]
    public void Get_KnownIdentity_DoesNotWriteToTable()
    {
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));

        function.FunctionHandler(GetRequest(Identity), new TestLambdaContext());

        tableMock.Verify(t => t.PutItemAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Post_NewIdentity_CreatesEntryWithAddressAndFreshTimestamps()
    {
        var (function, tableMock) = BuildFunction(null);

        var response = function.FunctionHandler(PostAddress(Identity, Address), new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        var body = JsonConvert.DeserializeAnonymousType(response.Body, new
        {
            identity = "",
            address = "",
            last_reported = "",
            last_changed = "",
            created = false
        });
        Assert.Equal(Identity, body!.identity);
        Assert.Equal(Address, body.address);
        Assert.Equal(Now, body.last_reported);
        Assert.Equal(Now, body.last_changed);
        Assert.True(body.created);
        tableMock.Verify(t => t.PutItemAsync(It.Is<Document>(d =>
            d["identity"].AsString() == Identity &&
            d["address"].AsString() == Address &&
            d["last_reported"].AsString() == Now &&
            d["last_changed"].AsString() == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Post_SameAddress_AdvancesLastReportedOnly()
    {
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));

        var response = function.FunctionHandler(PostAddress(Identity, Address), new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        var body = JsonConvert.DeserializeAnonymousType(response.Body, new
        {
            identity = "",
            address = "",
            last_reported = "",
            last_changed = "",
            created = true
        });
        Assert.Equal(Identity, body!.identity);
        Assert.Equal(Address, body.address);
        Assert.Equal(Now, body.last_reported);
        Assert.Equal(LastChanged, body.last_changed);
        Assert.False(body.created);
        tableMock.Verify(t => t.PutItemAsync(It.Is<Document>(d =>
            d["address"].AsString() == Address &&
            d["last_reported"].AsString() == Now &&
            d["last_changed"].AsString() == LastChanged), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Post_DifferentAddress_AdvancesLastReportedAndLastChanged()
    {
        const string newAddress = "198.51.100.7";
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));

        var response = function.FunctionHandler(PostAddress(Identity, newAddress), new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        var body = JsonConvert.DeserializeAnonymousType(response.Body, new
        {
            identity = "",
            address = "",
            last_reported = "",
            last_changed = "",
            created = true
        });
        Assert.Equal(Identity, body!.identity);
        Assert.Equal(newAddress, body.address);
        Assert.Equal(Now, body.last_reported);
        Assert.Equal(Now, body.last_changed);
        Assert.False(body.created);
        tableMock.Verify(t => t.PutItemAsync(It.Is<Document>(d =>
            d["address"].AsString() == newAddress &&
            d["last_reported"].AsString() == Now &&
            d["last_changed"].AsString() == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Post_RepeatedIdenticalSubmissions_LeaveStateEquivalentToSingleSubmission()
    {
        Document? stored = null;
        var tableMock = new Mock<ITable>();
        tableMock
            .Setup(t => t.GetItemAsync(It.IsAny<Primitive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        tableMock
            .Setup(t => t.PutItemAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => stored = doc);
        var function = new Function(tableMock.Object, Clock);

        function.FunctionHandler(PostAddress(Identity, Address), new TestLambdaContext());
        var first = stored!;

        function.FunctionHandler(PostAddress(Identity, Address), new TestLambdaContext());
        var second = stored!;

        Assert.Equal(first["address"].AsString(), second["address"].AsString());
        Assert.Equal(first["last_changed"].AsString(), second["last_changed"].AsString());
    }

    [Fact]
    public void Post_MissingAddress_ReturnsBadRequestWithoutWriting()
    {
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));

        var response = function.FunctionHandler(PostRequest(Identity, "{}"), new TestLambdaContext());

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("address is required", response.Body);
        tableMock.Verify(t => t.PutItemAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Put_ToIdentityRoute_IsNotHandledAndDoesNotWrite()
    {
        var (function, tableMock) = BuildFunction(Entry(Identity, Address));
        var request = GetRequest(Identity);
        request.HttpMethod = "PUT";

        var response = function.FunctionHandler(request, new TestLambdaContext());

        Assert.Equal(404, response.StatusCode);
        tableMock.Verify(t => t.PutItemAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
