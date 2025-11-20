using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PriceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PriceController(ApplicationDbContext context)
        {
            _context = context;
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(price);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Cập nhật thành công.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.PriceTables.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
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
            var price = await _context.PriceTables.FindAsync(id);
            if (price != null)
            {
                _context.PriceTables.Remove(price);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Xóa bảng giá thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}