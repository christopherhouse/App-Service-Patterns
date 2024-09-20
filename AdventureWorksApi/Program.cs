using System.Text.Json;
using AdventureWorksApi.Data;
using AdventureWorksApi.Data.Dto;
using AdventureWorksApi.Data.Models;
using AdventureWorksApi.Services;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using HealthStatus = AdventureWorksApi.Data.Dto.HealthStatus;

const int CACHE_MINUTES = 5;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AdventureWorksContext>(opt =>
    opt.UseSqlServer(builder.Configuration["adventureWorksConnectionString"]));
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<RedisCacheDependencyTracker>();
builder.Services.AddSingleton<MessagingClient>(sp =>
{
    var connectionString = builder.Configuration["serviceBusConnectionString"];

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new ArgumentException("Service Bus connection string was not found");
    }

    return new MessagingClient(connectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration["redisConnectionString"];

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new ArgumentException("Redis connection string was not found");
    }

    return ConnectionMultiplexer.Connect(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/api/customers/{id}", async Task<Results<Ok<Customer>, NotFound>> (int id, 
        [FromQuery(Name="useCache")]bool useCache,
        AdventureWorksContext db, 
        IConnectionMultiplexer redis,
        RedisCacheDependencyTracker tracker) => 
{
    Customer? customer;

    if (useCache)
    {
        var redisDb = redis.GetDatabase();
        var key = $"/api/customers/{id}";
        var customerJson = await tracker.ExecuteAndLogAsync(() => redisDb.StringGetAsync(key), "GetCustomerCache", key);

        if (string.IsNullOrEmpty(customerJson))
        {
            customer = await db.Customers.FindAsync(id);
            await tracker.ExecuteAndLogAsync(() => redisDb.StringSetAsync(key, JsonSerializer.Serialize(customer), TimeSpan.FromMinutes(CACHE_MINUTES)), "SetCustomerCache", key);
        }
        else
        {
            customer = JsonSerializer.Deserialize<Customer>(customerJson!);
        }
    }
    else
    {
        customer = await db.Customers.FindAsync(id);
    }

    return customer is not null ? TypedResults.Ok(customer) : TypedResults.NotFound();

}).WithName("GetCustomerById")
    .Produces<Ok<Customer>>(StatusCodes.Status200OK)
    .Produces<NotFound>(StatusCodes.Status404NotFound)
    .WithOpenApi();

app.MapGet("/api/customers", async (AdventureWorksContext db,
        IConnectionMultiplexer redis,
        RedisCacheDependencyTracker tracker,
        [FromQuery(Name = "pageNumber")]int pageNumber,
        [FromQuery(Name = "pageSize")]int pageSize) =>
{
    var redisDb = redis.GetDatabase();
    var key = $"/api/customers?pageNumber={pageNumber}&pageSize={pageSize}";
    PagedResult<Customer> customerList = null!;

    var cachedCustomers = await tracker.ExecuteAndLogAsync(() => redisDb.StringGetAsync(key), "GetCustomersCache", key);

    if (cachedCustomers.IsNullOrEmpty)
    {
        var skip = (pageNumber - 1) * pageSize;

        var customers = await db.Customers
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var totalCustomers = await db.Customers.CountAsync();

        customerList = new PagedResult<Customer>
        {
            TotalCount = totalCustomers,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = customers
        };

        await tracker.ExecuteAndLogAsync(() => redisDb.StringSetAsync(key, JsonSerializer.Serialize(customerList), TimeSpan.FromMinutes(CACHE_MINUTES)), "SetCustomersCache", key);
    }
    else
    {
        customerList = JsonSerializer.Deserialize<PagedResult<Customer>>(cachedCustomers!)!;
    }
    
    return customerList;

}).WithName("GetCustomers")
  .Produces<List<Customer>>(StatusCodes.Status200OK)
  .WithOpenApi();

app.MapPost("/api/orders", async (AdventureWorksApi.Data.Dto.OrderDto order, 
        MessagingClient messaging,
        IConfiguration config,
        AdventureWorksContext db,
        HttpContext http) =>
{
    var queue = config["queueName"];
    var statusFormat = config["orderStatusUriFormat"];
    var orderNumber = Guid.NewGuid().ToString();
    order.OrderNumber = orderNumber;

    var orderJson = JsonSerializer.Serialize(order);

    if (string.IsNullOrWhiteSpace(statusFormat))
    {
        throw new ArgumentException("Order status URI format was not found");   
    }

    if (string.IsNullOrWhiteSpace(queue))
    {
        throw new ArgumentException("Queue name was not found");
    }

    await messaging.SendMessageAsync(queue, orderJson);

    var status = new OrderStatus
    {
        CustomerId = order.CustomerId,
        OrderNumber = orderNumber,
        Status = "Received",
        DateModified = DateTime.UtcNow
    };

    await db.OrderStatuses.AddAsync(status);
    await db.SaveChangesAsync();
    
    http.Response.Headers.Append("Location", string.Format(statusFormat, orderNumber));

    return Results.Accepted();
})
    .WithName("CreateOrder")
    .Produces(StatusCodes.Status202Accepted)
    .WithOpenApi();

app.MapGet("/api/orders/status/{orderNumber}", async Task<Results<Ok<OrderStatus>, NotFound>> (string orderNumber, 
        AdventureWorksContext db,
        IConnectionMultiplexer redis,
        RedisCacheDependencyTracker tracker) =>
    {
        OrderStatus? status;

        var cacheKey = $"/api/orders/status/{orderNumber}";
        var redisDb = redis.GetDatabase();
        var statusJson = await tracker.ExecuteAndLogAsync(() => redisDb.StringGetAsync(cacheKey), "GetStatusCache", cacheKey);

        if (!string.IsNullOrWhiteSpace(statusJson))
        {
            status = JsonSerializer.Deserialize<OrderStatus>(statusJson!)!;
        }
        else
        {
            status = await db.OrderStatuses.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            statusJson = JsonSerializer.Serialize(status);
            await tracker.ExecuteAndLogAsync(() => redisDb.StringSetAsync(cacheKey, statusJson, TimeSpan.FromMinutes(CACHE_MINUTES)), "SetStatusCache", cacheKey);
        }


        return status is not null ? TypedResults.Ok(status) : TypedResults.NotFound();

    }).WithName("GetOrderStatus")
    .Produces<OrderStatus>(StatusCodes.Status200OK)
    .Produces<NotFound>(StatusCodes.Status404NotFound)
    .WithOpenApi();

app.MapGet("/api/health", async (AdventureWorksContext db,
    IConnectionMultiplexer redis,
    IConfiguration configuration) =>
    {
        var sqlHealthy = false;
        var redisHealthy = false;
        var serviceBusHealthy = false;
        var serviceBusConnectionString = configuration["serviceBusConnectionString"];

        if (string.IsNullOrEmpty(serviceBusConnectionString))
        {
            throw new ArgumentException("Service Bus connection string was not found");
        }

        try
        {
            await db.Customers.CountAsync();
            sqlHealthy = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        try
        {
            redisHealthy = redis.IsConnected;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        try
        {
            var sbClient = new ServiceBusClient(serviceBusConnectionString);
            var qClient = sbClient.CreateReceiver(configuration["queueName"]);
            await qClient.PeekMessageAsync();

            serviceBusHealthy = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        var response = new HealthStatus
        {
            SqlHealthy = sqlHealthy,
            RedisHealthy = redisHealthy,
            ServiceBusHealthy = serviceBusHealthy,
            KeyVaultHealthy = sqlHealthy && redisHealthy && serviceBusHealthy
        };

        return response;
    }).WithName("HealthCheck")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status500InternalServerError)
    .WithOpenApi();

app.Run();

