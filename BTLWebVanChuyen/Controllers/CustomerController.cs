using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
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

        // GET: /Customer
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Include(c => c.User)
                .ToListAsync();
            return View(customers);
        }

        // POST: /Customer/Promote/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promote(int id, string role)
        {
            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();

            var user = customer.User;
            await _userManager.RemoveFromRoleAsync(user, "Customer");
            await _userManager.AddToRoleAsync(user, role);

            // Tạo Employee
            var employee = new Employee
            {
                UserId = user.Id,
                Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper
            };
            _context.Employees.Add(employee);
            _context.Customers.Remove(customer);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Customer/Delete/{id}
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: /Customer/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                var user = await _userManager.FindByIdAsync(customer.UserId);
                if (user != null)
                    await _userManager.DeleteAsync(user);

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
