using AdventureWorksApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AdventureWorksApi.Data;

public class AdventureWorksContext : DbContext
{
    public AdventureWorksContext(DbContextOptions<AdventureWorksContext> options)
        : base(options)
    {
    }

    public DbSet<Models.Customer> Customers { get; set; }

    public DbSet<Order> Orders { get; set; }

    public DbSet<OrderLineItem> OrderLineItems { get; set; }

    public DbSet<OrderStatus> OrderStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Customer>(entity =>
        {
            entity.ToTable("Customer", "SalesLT");

            entity.HasKey(e => e.CustomerID);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("OrderHeaders", "dbo");

            entity.HasKey(e => e.OrderId);
        });

        modelBuilder.Entity<OrderLineItem>(entity =>
        {
            entity.ToTable("OrderLineItems", "dbo");

            entity.HasKey(e => e.OrderLineItemId);
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.ToTable("OrderStatuses", "dbo");

            entity.HasKey(e => e.OrderStatusId);
        });
    }
}
