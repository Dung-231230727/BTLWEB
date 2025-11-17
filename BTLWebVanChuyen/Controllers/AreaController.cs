using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class AreaController : Controller
{
    private readonly ApplicationDbContext _context;
    public AreaController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.Areas.ToListAsync());

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Area area)
    {
        if (!ModelState.IsValid) return View(area);
        _context.Areas.Add(area);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null) return NotFound();
        return View(area);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Area area)
    {
        if (!ModelState.IsValid) return View(area);
        _context.Update(area);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null) return NotFound();
        return View(area);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area != null)
        {
            _context.Areas.Remove(area);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
