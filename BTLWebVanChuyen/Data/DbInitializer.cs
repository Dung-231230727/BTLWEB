using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.MigrateAsync();

            // 1. Seed Roles
            string[] roles = new[] { "Admin", "Customer", "Dispatcher", "Shipper" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2. Seed Admin user
            string adminEmail = "admin@webvanchuyen.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Admin System",
                    IsCustomer = false,
                    IsEmployee = false
                };

                await userManager.CreateAsync(admin, "Admin@123"); // password mặc định
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // 3. Seed sample Customers
            if (!context.Customers.Any())
            {
                for (int i = 1; i <= 3; i++)
                {
                    string email = $"customer{i}@webvanchuyen.com";
                    if (await userManager.FindByEmailAsync(email) == null)
                    {
                        var user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            FullName = $"Customer {i}",
                            IsCustomer = true,
                            IsEmployee = false
                        };
                        await userManager.CreateAsync(user, "Customer@123");
                        await userManager.AddToRoleAsync(user, "Customer");

                        var customer = new Customer
                        {
                            UserId = user.Id,
                            Address = $"Địa chỉ {i}"
                        };
                        context.Customers.Add(customer);
                    }
                }
                await context.SaveChangesAsync();
            }

            // 4. Seed sample Employees
            if (!context.Employees.Any())
            {
                // Dispatcher
                string dispatcherEmail = "dispatcher@webvanchuyen.com";
                if (await userManager.FindByEmailAsync(dispatcherEmail) == null)
                {
                    var dispatcherUser = new ApplicationUser
                    {
                        UserName = dispatcherEmail,
                        Email = dispatcherEmail,
                        FullName = "Người điều phối",
                        IsCustomer = false,
                        IsEmployee = true
                    };
                    await userManager.CreateAsync(dispatcherUser, "Dispatcher@123");
                    await userManager.AddToRoleAsync(dispatcherUser, "Dispatcher");

                    context.Employees.Add(new Employee
                    {
                        UserId = dispatcherUser.Id,
                        Role = EmployeeRole.Dispatcher
                    });
                }

                // Shipper
                string shipperEmail = "shipper@webvanchuyen.com";
                if (await userManager.FindByEmailAsync(shipperEmail) == null)
                {
                    var shipperUser = new ApplicationUser
                    {
                        UserName = shipperEmail,
                        Email = shipperEmail,
                        FullName = "Nhân viên giao hàng",
                        IsCustomer = false,
                        IsEmployee = true
                    };
                    await userManager.CreateAsync(shipperUser, "Shipper@123");
                    await userManager.AddToRoleAsync(shipperUser, "Shipper");

                    context.Employees.Add(new Employee
                    {
                        UserId = shipperUser.Id,
                        Role = EmployeeRole.Shipper
                    });
                }

                await context.SaveChangesAsync();
            }

            // 5. Optional: Seed Areas
            if (!context.Areas.Any())
            {
                context.Areas.AddRange(
                    new Area { AreaName = "Hà Nội", AreaCode = "HN" },
                    new Area { AreaName = "Hồ Chí Minh", AreaCode = "HCM" },
                    new Area { AreaName = "Đà Nẵng", AreaCode = "DN" }
                );
                await context.SaveChangesAsync();
            }

            // 6. Optional: Seed PriceTable
            if (!context.PriceTables.Any())
            {
                var areas = context.Areas.ToList();
                foreach (var area in areas)
                {
                    context.PriceTables.Add(new PriceTable
                    {
                        AreaId = area.AreaId,
                        BasePrice = 10000,
                        PricePerKm = 5000,
                        WeightPrice = 2000
                    });
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
