using System;
using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AddressRegistry
{
    public class Function
    {
        private static readonly string TableName = Environment.GetEnvironmentVariable("DDB_TABLE");
        private static readonly AmazonDynamoDBClient DdbClient = new AmazonDynamoDBClient();
        private static readonly ITable DefaultDdbTable =
            Amazon.DynamoDBv2.DocumentModel.Table.LoadTable(DdbClient, TableName);
        private static readonly Func<DateTime> DefaultClock = () => DateTime.UtcNow;

        private readonly ITable _ddbTable;
        private readonly Func<DateTime> _clock;

        public Function() : this(DefaultDdbTable, DefaultClock) { }

        public Function(ITable ddbTable, Func<DateTime> clock)
        {
            _ddbTable = ddbTable;
            _clock = clock;
        }

        public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var proxy = request.PathParameters?["proxy"] ?? "";
            var parts = proxy.Split('/', StringSplitOptions.RemoveEmptyEntries);

            context.Logger.LogLine($"Received {request.HttpMethod} for {request.Path}");
            if (parts.Length == 2 && parts[0] == "identity")
            {
                if (request.HttpMethod == "GET")
                    return Lookup(parts[1]);
                if (request.HttpMethod == "POST")
                    return Register(parts[1], request.Body ?? "");
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 404,
                Body = "Not Found"
            };
        }

        private APIGatewayProxyResponse Lookup(string identity)
        {
            var doc = _ddbTable.GetItemAsync(identity).Result;
            if (doc == null)
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Body = JsonConvert.SerializeObject(new { identity, found = false }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };

            var body = JsonConvert.SerializeObject(new
            {
                identity,
                address = doc["address"].AsString(),
                last_reported = doc["last_reported"].AsString(),
                last_changed = doc["last_changed"].AsString()
            });
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = body,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        private APIGatewayProxyResponse Register(string identity, string body)
        {
            string address;
            try
            {
                address = JsonConvert.DeserializeAnonymousType(body, new { address = "" })?.address ?? "";
            }
            catch (JsonException)
            {
                address = "";
            }

            if (address.Length == 0)
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = JsonConvert.SerializeObject(new { error = "address is required" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };

            var now = _clock().ToString("o");
            var stored = _ddbTable.GetItemAsync(identity).Result;
            var lastChanged = stored != null && stored["address"].AsString() == address
                ? stored["last_changed"].AsString()
                : now;

            _ddbTable.PutItemAsync(new Document
            {
                ["identity"] = identity,
                ["address"] = address,
                ["last_reported"] = now,
                ["last_changed"] = lastChanged
            }).Wait();

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(new
                {
                    identity,
                    address,
                    last_reported = now,
                    last_changed = lastChanged,
                    created = stored == null
                }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}
