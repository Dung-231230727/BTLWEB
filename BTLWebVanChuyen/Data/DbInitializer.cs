using BTLWebVanChuyen.Models;
using BTLWebVanChuyen.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Cập nhật Database
            await context.Database.MigrateAsync();

            // 2. Seed Roles
            string[] roles = { "Admin", "Dispatcher", "Shipper", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 3. Seed Admin
            var adminEmail = "admin@webvanchuyen.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Quản Trị Viên Hệ Thống",
                    PhoneNumber = "0988888888",
                    EmailConfirmed = true,
                    IsCustomer = false
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 4. Seed Areas (3 Khu vực)
            if (!context.Areas.Any())
            {
                context.Areas.AddRange(
                    new Area { AreaName = "Hà Nội" },
                    new Area { AreaName = "Hồ Chí Minh" },
                    new Area { AreaName = "Đà Nẵng" }
                );
                await context.SaveChangesAsync();
            }

            // 5. Seed Warehouses (3 Kho hàng)
            if (!context.Warehouses.Any())
            {
                var hn = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Hà Nội");
                var hcm = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Hồ Chí Minh");
                var dn = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Đà Nẵng");

                var warehouses = new List<Warehouse>();
                if (hn != null)
                {
                    warehouses.Add(new Warehouse { Name = "Kho Cầu Giấy", Address = "123 Cầu Giấy, HN", AreaId = hn.AreaId });
                    warehouses.Add(new Warehouse { Name = "Kho Đống Đa", Address = "45 Thái Hà, HN", AreaId = hn.AreaId });
                }
                if (hcm != null)
                {
                    warehouses.Add(new Warehouse { Name = "Kho Quận 1", Address = "Bến Thành, Q1, HCM", AreaId = hcm.AreaId });
                    warehouses.Add(new Warehouse { Name = "Kho Thủ Đức", Address = "Xa Lộ Hà Nội, HCM", AreaId = hcm.AreaId });
                }
                if (dn != null)
                {
                    warehouses.Add(new Warehouse { Name = "Kho Hải Châu", Address = "Bạch Đằng, ĐN", AreaId = dn.AreaId });
                }
                context.Warehouses.AddRange(warehouses);
                await context.SaveChangesAsync();
            }

            // 6. Seed PriceTables (3 Bảng giá)
            if (!context.PriceTables.Any())
            {
                var areas = await context.Areas.ToListAsync();
                foreach (var area in areas)
                {
                    context.PriceTables.Add(new PriceTable
                    {
                        AreaId = area.AreaId,
                        BasePrice = 15000,
                        PricePerKm = 5000,
                        WeightPrice = 2000
                    });
                }
                await context.SaveChangesAsync();
            }

            // ==================================================
            // 7. Seed Employees (3 Dispatcher + 3 Shipper)
            // ==================================================

            // Lấy Area từ DB để đảm bảo ID chính xác
            var areaHN = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Hà Nội");
            var areaHCM = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Hồ Chí Minh");
            var areaDN = await context.Areas.FirstOrDefaultAsync(a => a.AreaName == "Đà Nẵng");

            // Danh sách Dispatcher mẫu
            var dispatcherData = new[]
            {
                new { Email = "dispatcher.hn@webvanchuyen.com", Name = "Điều Phối Viên Hà Nội", Area = areaHN },
                new { Email = "dispatcher.hcm@webvanchuyen.com", Name = "Điều Phối Viên HCM", Area = areaHCM },
                new { Email = "dispatcher.dn@webvanchuyen.com", Name = "Điều Phối Viên Đà Nẵng", Area = areaDN }
            };

            foreach (var item in dispatcherData)
            {
                if (item.Area != null && await userManager.FindByEmailAsync(item.Email) == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        FullName = item.Name,
                        EmailConfirmed = true,
                        IsCustomer = false
                    };
                    var res = await userManager.CreateAsync(user, "Dispatcher@123");
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Dispatcher");
                        context.Employees.Add(new Employee
                        {
                            UserId = user.Id,
                            Role = EmployeeRole.Dispatcher,
                            AreaId = item.Area.AreaId
                        });
                    }
                }
            }

            // Danh sách Shipper mẫu
            var shipperData = new[]
            {
                new { Email = "shipper.hn@webvanchuyen.com", Name = "Shipper Hà Nội 1", Area = areaHN },
                new { Email = "shipper.hcm@webvanchuyen.com", Name = "Shipper Sài Gòn 1", Area = areaHCM },
                new { Email = "shipper.dn@webvanchuyen.com", Name = "Shipper Đà Nẵng 1", Area = areaDN }
            };

            foreach (var item in shipperData)
            {
                if (item.Area != null && await userManager.FindByEmailAsync(item.Email) == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        FullName = item.Name,
                        EmailConfirmed = true,
                        IsCustomer = false
                    };
                    var res = await userManager.CreateAsync(user, "Shipper@123");
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Shipper");
                        context.Employees.Add(new Employee
                        {
                            UserId = user.Id,
                            Role = EmployeeRole.Shipper,
                            AreaId = item.Area.AreaId
                        });
                    }
                }
            }
            await context.SaveChangesAsync();

            // ==================================================
            // 8. Seed Customers (3 Khách hàng)
            // ==================================================
            var customerData = new[]
            {
                new { Email = "khach1@gmail.com", Name = "Nguyễn Văn A", Address = "123 Cầu Giấy, Hà Nội" },
                new { Email = "khach2@gmail.com", Name = "Trần Thị B", Address = "456 Lê Lợi, Quận 1, HCM" },
                new { Email = "khach3@gmail.com", Name = "Lê Văn C", Address = "789 Nguyễn Văn Linh, Đà Nẵng" }
            };

            foreach (var item in customerData)
            {
                if (await userManager.FindByEmailAsync(item.Email) == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        FullName = item.Name,
                        PhoneNumber = "090000000" + new Random().Next(1, 9),
                        EmailConfirmed = true,
                        IsCustomer = true
                    };
                    var res = await userManager.CreateAsync(user, "Customer@123");
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Customer");
                        context.Customers.Add(new Customer
                        {
                            UserId = user.Id,
                            Address = item.Address
                        });
                    }
                }
            }
            await context.SaveChangesAsync();
        }
    }
}