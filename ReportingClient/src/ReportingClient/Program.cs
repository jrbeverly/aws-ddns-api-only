using ReportingClient;

ClientConfig config;
try
{
    config = ClientConfig.FromEnvironment();
}
catch (InvalidOperationException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

using var http = new HttpClient();
Func<Task<string>> resolveAddress = config.Address != null
    ? () => Task.FromResult(config.Address)
    : async () => await http.GetStringAsync("https://checkip.amazonaws.com/");

var reporter = new Reporter(http, config.Endpoint, config.Identity, resolveAddress);

while (true)
{
    var result = await reporter.ReportAsync();
    var outcome = result.Status switch
    {
        ReportStatus.Created => "result=created",
        ReportStatus.Updated => "result=updated",
        _ => $"result=failed error={result.Detail}"
    };
    Console.WriteLine($"{DateTime.UtcNow:o} identity={result.Identity} address={result.Address} {outcome}");
    await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds));
}
