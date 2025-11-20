using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class PriceController : Controller
{
    private readonly ApplicationDbContext _context;
    public PriceController(ApplicationDbContext context) => _context = context;

    [Authorize(Roles = "Admin,Customer")]
    public async Task<IActionResult> Index() => View(await _context.PriceTables.Include(p => p.Area).ToListAsync());

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewBag.Areas = _context.Areas.ToList();
        return View();
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(PriceTable price)
    {
        if (!ModelState.IsValid) return View(price);
        _context.PriceTables.Add(price);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var price = await _context.PriceTables.FindAsync(id);
        if (price == null) return NotFound();

        ViewBag.Areas = _context.Areas.ToList();
        
        return View(price);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, PriceTable price)
    {
        if (!ModelState.IsValid) {
            ViewBag.Areas = _context.Areas.ToList();
            return View(price);
        } 
        
        _context.Update(price);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var price = await _context.PriceTables.Include(p => p.Area).FirstOrDefaultAsync(p => p.Id == id);
        if (price == null) return NotFound();
        return View(price);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var price = await _context.PriceTables.FindAsync(id);
        if (price != null)
        {
            _context.PriceTables.Remove(price);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
