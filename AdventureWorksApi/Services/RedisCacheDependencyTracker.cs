using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;


namespace AdventureWorksApi.Services;

public class RedisCacheDependencyTracker
{
    private readonly TelemetryClient _telemetryClient;

    public RedisCacheDependencyTracker(TelemetryClient client)
    {
        _telemetryClient = client;
    }

    public async Task<T> ExecuteAndLogAsync<T>(Func<Task<T>> redisMethod, string opName, string key)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>(opName);
        operation.Telemetry.Name = "Redis";
        operation.Telemetry.Type = "Redis";
        operation.Telemetry.Data = $"{opName}: {key}";

        try
        {
            return await redisMethod.Invoke();
        }
        catch
        {
            operation.Telemetry.Success = false;
            throw;
        }
    }

    public async Task ExecuteAndLogAsync(Func<Task> redisMethod, string opName, string key)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>(opName);
        operation.Telemetry.Name = "Redis";
        operation.Telemetry.Type = "Redis";
        operation.Telemetry.Data = $"{opName}: {key}";

        try
        {
            await redisMethod.Invoke();
        }
        catch
        {
            operation.Telemetry.Success = false;
            throw;
        }
    }
}
