using System.Text.Json;
using AdventureWorksApi.Data;
using AdventureWorksApi.Data.Models;
using AdventureWorksApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

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
        //var customerJson = await redisDb.StringGetAsync(key);
        var customerJson = await tracker.ExecuteAndLogAsync(() => redisDb.StringGetAsync(key), "GetCustomerCache");

        if (string.IsNullOrEmpty(customerJson))
        {
            customer = await db.Customers.FindAsync(id);
            await tracker.ExecuteAndLogAsync(() => redisDb.StringSetAsync(key, JsonSerializer.Serialize(customer), TimeSpan.FromMinutes(1)), "SetCustomerCache");
            //await redisDb.StringSetAsync(key, JsonSerializer.Serialize(customer), TimeSpan.FromMinutes(1));
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

app.MapGet("/api/customers", async (AdventureWorksContext db, IConnectionMultiplexer redis) =>
{
    var redisDb = redis.GetDatabase();
    var key = "/api/customers";
    List<Customer> customerList = null!;

    var cachedCustomers = await redisDb.StringGetAsync(key);

    if (cachedCustomers.IsNullOrEmpty)
    {
        customerList = await db.Customers.Take(5).ToListAsync();
        await redisDb.StringSetAsync(key, JsonSerializer.Serialize(customerList), TimeSpan.FromMinutes(1));
    }
    else
    {
        customerList = JsonSerializer.Deserialize<List<Customer>>(cachedCustomers!)!;
    }
    
    return customerList;

}).WithName("GetCustomers")
  .Produces<List<Customer>>(StatusCodes.Status200OK)
  .WithOpenApi();

app.MapPost("/api/orders", async (AdventureWorksApi.Data.Models.Order order, MessagingClient messaging,
    IConfiguration config) =>
{
    var orderJson = JsonSerializer.Serialize(order);
    var queue = config["queueName"];

    if (string.IsNullOrWhiteSpace(queue))
    {
        throw new ArgumentException("Queue name was not found");
    }

    await messaging.SendMessageAsync(queue, orderJson);

    return Results.Accepted();
})
    .WithName("CreateOrder")
    .Produces(StatusCodes.Status202Accepted)
    .WithOpenApi();

app.MapGet("/api/orders/status/{orderId}", () =>
    {

    }).WithName("GetOrderStatus")
    .Produces(StatusCodes.Status200OK)
    .WithOpenApi();

app.Run();

