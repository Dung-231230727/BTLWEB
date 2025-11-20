using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AreaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AreaController(ApplicationDbContext context)
        {
            _context = context;
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
                    _context.Update(area);
                    await _context.SaveChangesAsync();
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

            _context.Areas.Remove(area);
            await _context.SaveChangesAsync();
            TempData["Message"] = "Xóa khu vực thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}