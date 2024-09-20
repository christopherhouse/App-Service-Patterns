namespace AdventureWorksApi.Data.Dto;

public class HealthStatus
{
    public bool SqlHealthy { get; set; }
    public bool RedisHealthy { get; set; }
    public bool ServiceBusHealthy { get; set; }
    public bool KeyVaultHealthy { get; set; }
}
