using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")] // Chỉ Admin mới có quyền truy cập
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WarehouseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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

        // ==========================================
        // 1. INDEX: Danh sách + Tìm kiếm + Lọc
        // ==========================================
        public async Task<IActionResult> Index(string searchString, int? filterAreaId)
        {
            // Khởi tạo query lấy Kho kèm thông tin Khu vực
            var query = _context.Warehouses.Include(w => w.Area).AsQueryable();

            // 1. Logic Tìm kiếm (theo tên kho hoặc địa chỉ)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(w => w.Name.Contains(searchString) || w.Address.Contains(searchString));
            }

            // 2. Logic Lọc theo Khu vực
            if (filterAreaId.HasValue)
            {
                query = query.Where(w => w.AreaId == filterAreaId);
            }

            // Lưu lại giá trị search/filter để hiển thị lại trên View
            ViewData["CurrentFilter"] = searchString;
            // Tạo danh sách dropdown cho bộ lọc khu vực
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", filterAreaId);

            return View(await query.ToListAsync());
        }

        // ==========================================
        // 2. DETAILS: Xem chi tiết
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .Include(w => w.Area)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        // ==========================================
        // 3. CREATE: Thêm mới
        // ==========================================
        public IActionResult Create()
        {
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,AreaId")] Warehouse warehouse)
        {
            // 1. Kiểm tra trùng tên kho trong cùng một khu vực
            bool isDuplicate = await _context.Warehouses.AnyAsync(w => w.Name == warehouse.Name && w.AreaId == warehouse.AreaId);
            if (isDuplicate)
            {
                ModelState.AddModelError("Name", "Tên kho này đã tồn tại trong khu vực đã chọn.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(warehouse);
                await _context.SaveChangesAsync();
                
                // Thông báo cho admin
                var adminUserIds = await GetAdminUserIdsAsync();
                var areaName = await _context.Areas.Where(a => a.AreaId == warehouse.AreaId).Select(a => a.AreaName).FirstOrDefaultAsync();
                await CreateNotificationsAsync(adminUserIds, 
                    $"Kho hàng '{warehouse.Name}' đã được thêm mới tại khu vực '{areaName}'.");
                
                // Thông báo cho dispatchers và shippers trong khu vực này
                var employeeUserIds = await _context.Employees
                    .Where(e => e.AreaId == warehouse.AreaId)
                    .Select(e => e.UserId)
                    .ToListAsync();
                if (employeeUserIds.Any())
                {
                    await CreateNotificationsAsync(employeeUserIds,
                        $"Kho hàng mới '{warehouse.Name}' đã được thêm vào khu vực làm việc của bạn.");
                }
                
                TempData["Message"] = "Thêm kho hàng thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", warehouse.AreaId);
            return View(warehouse);
        }

        // ==========================================
        // 4. EDIT: Chỉnh sửa
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null) return NotFound();

            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", warehouse.AreaId);
            return View(warehouse);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,AreaId")] Warehouse warehouse)
        {
            if (id != warehouse.Id) return NotFound();

            // 1. Kiểm tra trùng tên (trừ chính bản ghi đang sửa)
            bool isDuplicate = await _context.Warehouses.AnyAsync(w => w.Name == warehouse.Name && w.AreaId == warehouse.AreaId && w.Id != id);
            if (isDuplicate)
            {
                ModelState.AddModelError("Name", "Tên kho này đã tồn tại trong khu vực đã chọn.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(warehouse);
                    await _context.SaveChangesAsync();
                    
                    // Thông báo cho admin
                    var adminUserIds = await GetAdminUserIdsAsync();
                    var areaName = await _context.Areas.Where(a => a.AreaId == warehouse.AreaId).Select(a => a.AreaName).FirstOrDefaultAsync();
                    await CreateNotificationsAsync(adminUserIds, 
                        $"Kho hàng '{warehouse.Name}' đã được cập nhật tại khu vực '{areaName}'.");
                    
                    // Thông báo cho dispatchers và shippers trong khu vực này
                    var employeeUserIds = await _context.Employees
                        .Where(e => e.AreaId == warehouse.AreaId)
                        .Select(e => e.UserId)
                        .ToListAsync();
                    if (employeeUserIds.Any())
                    {
                        await CreateNotificationsAsync(employeeUserIds,
                            $"Kho hàng '{warehouse.Name}' trong khu vực làm việc của bạn đã được cập nhật.");
                    }
                    
                    TempData["Message"] = "Cập nhật thông tin kho thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WarehouseExists(warehouse.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", warehouse.AreaId);
            return View(warehouse);
        }

        // ==========================================
        // 5. DELETE: Xóa kho
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .Include(w => w.Area)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null) return NotFound();

            // 1. Kiểm tra ràng buộc dữ liệu: Có đơn hàng nào đang dùng kho này không?
            // Kiểm tra cả Kho Lấy (PickupWarehouseId) và Kho Giao (DeliveryWarehouseId)
            bool hasOrders = await _context.Orders.AnyAsync(o => o.PickupWarehouseId == id || o.DeliveryWarehouseId == id);

            if (hasOrders)
            {
                // Nếu có đơn hàng liên quan, không cho phép xóa
                TempData["Error"] = "Không thể xóa kho hàng này vì đang có đơn hàng liên quan. Vui lòng kiểm tra lại dữ liệu đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var warehouseName = warehouse.Name;
            var warehouseAreaId = warehouse.AreaId;
            
            // Thông báo cho dispatchers và shippers trong khu vực này trước khi xóa
            var employeeUserIds = await _context.Employees
                .Where(e => e.AreaId == warehouseAreaId)
                .Select(e => e.UserId)
                .ToListAsync();
            if (employeeUserIds.Any())
            {
                await CreateNotificationsAsync(employeeUserIds,
                    $"Kho hàng '{warehouseName}' trong khu vực làm việc của bạn đã bị xóa khỏi hệ thống.");
            }
            
            // Thông báo cho customers có đơn hàng liên quan đến kho này
            var customerUserIds = await _context.Orders
                .Where(o => o.PickupWarehouseId == id || o.DeliveryWarehouseId == id)
                .Select(o => o.Customer.UserId)
                .Distinct()
                .ToListAsync();
            if (customerUserIds.Any())
            {
                await CreateNotificationsAsync(customerUserIds,
                    $"Kho hàng '{warehouseName}' liên quan đến đơn hàng của bạn đã bị xóa khỏi hệ thống.");
            }
            
            _context.Warehouses.Remove(warehouse);
            await _context.SaveChangesAsync();
            
            // Thông báo cho admin
            var adminUserIds = await GetAdminUserIdsAsync();
            await CreateNotificationsAsync(adminUserIds, 
                $"Kho hàng '{warehouseName}' đã bị xóa khỏi hệ thống.");

            TempData["Message"] = "Đã xóa kho hàng thành công.";
            return RedirectToAction(nameof(Index));
        }

        private bool WarehouseExists(int id)
        {
            return _context.Warehouses.Any(e => e.Id == id);
        }
    }
}