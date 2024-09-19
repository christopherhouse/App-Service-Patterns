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

    public async Task<T> ExecuteAndLogAsync<T>(Func<Task<T>> redisMethod, string opName)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>(opName);
        operation.Telemetry.Name = "Redis";
        operation.Telemetry.Type = "Redis";

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

    public async Task ExecuteAndLogAsync(Func<Task> redisMethod, string opName)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>(opName);
        operation.Telemetry.Name = "Redis";
        operation.Telemetry.Type = "Redis";

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
