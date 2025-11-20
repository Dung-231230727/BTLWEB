using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<PriceTable> PriceTables { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLog> OrderLogs { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. User - Customer 1-1
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Customer)
                .WithOne(c => c.User)
                .HasForeignKey<Customer>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. User - Employee 1-1
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.User)
                .HasForeignKey<Employee>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. Area - Order
            builder.Entity<Order>()
                .HasOne(o => o.PickupArea)
                .WithMany(a => a.PickupOrders)
                .HasForeignKey(o => o.PickupAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Order>()
                .HasOne(o => o.DeliveryArea)
                .WithMany(a => a.DeliveryOrders)
                .HasForeignKey(o => o.DeliveryAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Area - PriceTable
            builder.Entity<PriceTable>()
                .HasOne(p => p.Area)
                .WithMany(a => a.PriceTables)
                .HasForeignKey(p => p.AreaId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Order - Dispatcher (Restrict để tránh multiple cascade)
            builder.Entity<Order>()
                .HasOne(o => o.Dispatcher)
                .WithMany(e => e.OrdersAsDispatcher)
                .HasForeignKey(o => o.DispatcherId)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. Order - Shipper (Restrict)
            builder.Entity<Order>()
                .HasOne(o => o.Shipper)
                .WithMany(e => e.OrdersAsShipper)
                .HasForeignKey(o => o.ShipperId)
                .OnDelete(DeleteBehavior.Restrict);

            // 7. Order - Customer (Cascade)
            builder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // 8. OrderLog - Order 1-n (Cascade)
            builder.Entity<OrderLog>()
                .HasOne(l => l.Order)
                .WithMany(o => o.OrderLogs)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
