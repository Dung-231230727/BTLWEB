using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin,Customer")]
    public class PriceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PriceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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

        // INDEX: Lọc theo khu vực
        public async Task<IActionResult> Index(int? filterAreaId)
        {
            var query = _context.PriceTables.Include(p => p.Area).AsQueryable();

            if (filterAreaId.HasValue)
            {
                query = query.Where(p => p.AreaId == filterAreaId);
            }

            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", filterAreaId);
            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var price = await _context.PriceTables.Include(p => p.Area).FirstOrDefaultAsync(m => m.Id == id);
            if (price == null) return NotFound();
            return View(price);
        }

        public IActionResult Create()
        {
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName");
            return View();
        }

        // CREATE: Check trùng (1 Khu vực chỉ có 1 bảng giá)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PriceTable price)
        {
            if (_context.PriceTables.Any(p => p.AreaId == price.AreaId))
            {
                ModelState.AddModelError("AreaId", "Khu vực này đã có bảng giá.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(price);
                await _context.SaveChangesAsync();
                
                // Thông báo cho admin
                var adminUserIds = await GetAdminUserIdsAsync();
                var areaName = await _context.Areas.Where(a => a.AreaId == price.AreaId).Select(a => a.AreaName).FirstOrDefaultAsync();
                await CreateNotificationsAsync(adminUserIds, 
                    $"Bảng giá cho khu vực '{areaName}' đã được tạo mới.");
                
                // Thông báo cho tất cả customers (vì ảnh hưởng đến giá)
                var customerUserIds = await _context.Customers.Select(c => c.UserId).ToListAsync();
                if (customerUserIds.Any())
                {
                    await CreateNotificationsAsync(customerUserIds,
                        $"Bảng giá vận chuyển đã được cập nhật. Vui lòng kiểm tra giá mới khi đặt hàng.");
                }
                
                TempData["Message"] = "Tạo bảng giá thành công.";
                return RedirectToAction(nameof(Index));
            }
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", price.AreaId);
            return View(price);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var price = await _context.PriceTables.FindAsync(id);
            if (price == null) return NotFound();

            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", price.AreaId);
            return View(price);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PriceTable price)
        {
            if (id != price.Id) return NotFound();

            // Check trùng nếu đổi khu vực (trừ chính nó ra)
            if (_context.PriceTables.Any(p => p.AreaId == price.AreaId && p.Id != id))
            {
                ModelState.AddModelError("AreaId", "Khu vực này đã có bảng giá khác.");
            }

            // If invalid, re-show view with errors (also add error summary for easier visibility)
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));
                if (!string.IsNullOrEmpty(errors)) ModelState.AddModelError(string.Empty, "Validation errors: " + errors);
                ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", price.AreaId);
                return View(price);
            }

            var existing = await _context.PriceTables.FindAsync(id);
            if (existing == null) return NotFound();

            // Copy posted values into tracked entity
            existing.AreaId = price.AreaId;
            existing.BasePrice = price.BasePrice;
            existing.PricePerKm = price.PricePerKm;
            existing.WeightPrice = price.WeightPrice;

            try
            {
                await _context.SaveChangesAsync();

                // Notifications (original behavior)
                var adminUserIds = await GetAdminUserIdsAsync();
                var areaName = await _context.Areas.Where(a => a.AreaId == existing.AreaId).Select(a => a.AreaName).FirstOrDefaultAsync();
                await CreateNotificationsAsync(adminUserIds,
                    $"Bảng giá cho khu vực '{areaName}' đã được cập nhật.");

                var customerUserIds = await _context.Customers.Select(c => c.UserId).ToListAsync();
                if (customerUserIds.Any())
                {
                    await CreateNotificationsAsync(customerUserIds,
                        $"Bảng giá vận chuyển đã được cập nhật. Vui lòng kiểm tra giá mới khi đặt hàng.");
                }

                TempData["Message"] = "Cập nhật thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException dbEx)
            {
                // Surface DB errors to the view
                ModelState.AddModelError(string.Empty, "Database error: " + dbEx.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Unexpected error: " + ex.GetBaseException().Message);
            }

            // On exception, re-show view and display errors in validation summary
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", price.AreaId);
            return View(price);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var price = await _context.PriceTables.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == id);
            if (price == null) return NotFound();
            return View(price);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var price = await _context.PriceTables.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == id);
            if (price != null)
            {
                var areaName = price.Area?.AreaName ?? "Khu vực";
                
                // Thông báo cho admin
                var adminUserIds = await GetAdminUserIdsAsync();
                await CreateNotificationsAsync(adminUserIds, 
                    $"Bảng giá cho khu vực '{areaName}' đã bị xóa khỏi hệ thống.");
                
                // Thông báo cho tất cả customers
                var customerUserIds = await _context.Customers.Select(c => c.UserId).ToListAsync();
                if (customerUserIds.Any())
                {
                    await CreateNotificationsAsync(customerUserIds,
                        $"Bảng giá vận chuyển cho khu vực '{areaName}' đã bị xóa. Vui lòng liên hệ để biết thêm thông tin.");
                }
                
                _context.PriceTables.Remove(price);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Xóa bảng giá thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
