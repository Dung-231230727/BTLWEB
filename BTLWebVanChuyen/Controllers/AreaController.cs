using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AreaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AreaController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper: Lấy tất cả admin users
        private async Task<List<string>> GetAdminUserIdsAsync()
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            return adminUsers.Select(u => u.Id).ToList();
        }

        // Helper: Tạo thông báo
        private async Task CreateNotificationAsync(string userId, string message, int? orderId = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                OrderId = orderId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });
            await _context.SaveChangesAsync();
        }

        // Helper: Tạo thông báo cho nhiều users
        private async Task CreateNotificationsAsync(IEnumerable<string> userIds, string message, int? orderId = null)
        {
            var recipients = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (!recipients.Any() || string.IsNullOrWhiteSpace(message)) return;

            foreach (var uid in recipients)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    OrderId = orderId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            await _context.SaveChangesAsync();
        }

        // INDEX: Tìm kiếm
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.Areas.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(a => a.AreaName.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var area = await _context.Areas
                .Include(a => a.Warehouses) // Load danh sách kho
                .FirstOrDefaultAsync(m => m.AreaId == id);
            if (area == null) return NotFound();
            return View(area);
        }

        public IActionResult Create() => View();

        // CREATE: Check trùng tên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AreaId,AreaName")] Area area)
        {
            if (_context.Areas.Any(a => a.AreaName == area.AreaName))
            {
                ModelState.AddModelError("AreaName", "Tên khu vực này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(area);
                await _context.SaveChangesAsync();
                
                // Thông báo cho admin
                var adminUserIds = await GetAdminUserIdsAsync();
                await CreateNotificationsAsync(adminUserIds, 
                    $"Khu vực '{area.AreaName}' đã được thêm mới vào hệ thống.");
                
                TempData["Message"] = "Thêm khu vực thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(area);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var area = await _context.Areas.FindAsync(id);
            if (area == null) return NotFound();
            return View(area);
        }

        // EDIT: Check trùng tên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AreaId,AreaName")] Area area)
        {
            if (id != area.AreaId) return NotFound();

            if (_context.Areas.Any(a => a.AreaName == area.AreaName && a.AreaId != id))
            {
                ModelState.AddModelError("AreaName", "Tên khu vực này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var oldArea = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.AreaId == id);
                    _context.Update(area);
                    await _context.SaveChangesAsync();
                    
                    // Thông báo cho admin
                    var adminUserIds = await GetAdminUserIdsAsync();
                    await CreateNotificationsAsync(adminUserIds, 
                        $"Khu vực '{area.AreaName}' đã được cập nhật.");
                    
                    // Thông báo cho employees trong khu vực này
                    var employeeUserIds = await _context.Employees
                        .Where(e => e.AreaId == area.AreaId)
                        .Select(e => e.UserId)
                        .ToListAsync();
                    if (employeeUserIds.Any())
                    {
                        await CreateNotificationsAsync(employeeUserIds,
                            $"Khu vực làm việc của bạn '{area.AreaName}' đã được cập nhật.");
                    }
                    
                    TempData["Message"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Areas.Any(e => e.AreaId == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(area);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var area = await _context.Areas.FirstOrDefaultAsync(m => m.AreaId == id);
            if (area == null) return NotFound();
            return View(area);
        }

        // DELETE: Check ràng buộc dữ liệu
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var area = await _context.Areas.FindAsync(id);
            if (area == null) return NotFound();

            // Kiểm tra các bảng liên quan
            bool hasWarehouses = await _context.Warehouses.AnyAsync(w => w.AreaId == id);
            bool hasEmployees = await _context.Employees.AnyAsync(e => e.AreaId == id);
            bool hasPrices = await _context.PriceTables.AnyAsync(p => p.AreaId == id);
            bool hasOrders = await _context.Orders.AnyAsync(o => o.PickupAreaId == id || o.DeliveryAreaId == id);

            if (hasWarehouses || hasEmployees || hasPrices || hasOrders)
            {
                TempData["Error"] = "Không thể xóa khu vực này vì đang chứa dữ liệu quan trọng (Kho, Nhân viên, Đơn hàng...).";
                return RedirectToAction(nameof(Index));
            }

            var areaName = area.AreaName;
            
            // Thông báo cho employees trong khu vực này trước khi xóa
            var employeeUserIds = await _context.Employees
                .Where(e => e.AreaId == id)
                .Select(e => e.UserId)
                .ToListAsync();
            if (employeeUserIds.Any())
            {
                await CreateNotificationsAsync(employeeUserIds,
                    $"Khu vực làm việc của bạn '{areaName}' đã bị xóa khỏi hệ thống.");
            }
            
            // Thông báo cho customers có đơn hàng trong khu vực này
            var customerUserIds = await _context.Orders
                .Where(o => o.PickupAreaId == id || o.DeliveryAreaId == id)
                .Select(o => o.Customer.UserId)
                .Distinct()
                .ToListAsync();
            if (customerUserIds.Any())
            {
                await CreateNotificationsAsync(customerUserIds,
                    $"Khu vực '{areaName}' liên quan đến đơn hàng của bạn đã bị xóa khỏi hệ thống.");
            }
            
            _context.Areas.Remove(area);
            await _context.SaveChangesAsync();
            
            // Thông báo cho admin
            var adminUserIds = await GetAdminUserIdsAsync();
            await CreateNotificationsAsync(adminUserIds, 
                $"Khu vực '{areaName}' đã bị xóa khỏi hệ thống.");
            
            TempData["Message"] = "Xóa khu vực thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}