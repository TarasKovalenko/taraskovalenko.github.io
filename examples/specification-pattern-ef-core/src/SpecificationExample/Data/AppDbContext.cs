using Microsoft.EntityFrameworkCore;
using SpecificationExample.Domain;

namespace SpecificationExample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .HasOne(order => order.Customer)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerId);

        modelBuilder.Entity<Order>()
            .HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId);

        modelBuilder.Entity<Order>()
            .HasMany(order => order.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.OrderId);
    }
}
