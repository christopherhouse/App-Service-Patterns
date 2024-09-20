using AdventureWorksApi.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddDbContext<AdventureWorksContext>(opt =>
            opt.UseSqlServer(Environment.GetEnvironmentVariable("adventureWorksConnectionString")));
    })
    .Build();

host.Run();
