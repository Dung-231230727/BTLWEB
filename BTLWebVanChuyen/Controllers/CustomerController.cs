using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var customers = await _context.Customers.Include(c => c.User).ToListAsync();
        return View(customers);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer != null)
        {
            var user = await _userManager.FindByIdAsync(customer.UserId);
            if (user != null) await _userManager.DeleteAsync(user);

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
