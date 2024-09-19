using System.Text.Json;
using AdventureWorksApi.Data;
using AdventureWorksApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AdventureWorksContext>(opt =>
    opt.UseSqlServer(builder.Configuration["adventureWorksConnectionString"]));

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
  .WithOpenApi();


app.Run();

