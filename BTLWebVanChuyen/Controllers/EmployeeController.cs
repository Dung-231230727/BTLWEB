using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Employee
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.User)
                .ToListAsync();
            return View(employees);
        }

        // GET: /Employee/Create
        public IActionResult Create() => View();

        // POST: /Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, string role)
        {
            if (!ModelState.IsValid) return View(employee);

            // Tạo ApplicationUser
            var user = new ApplicationUser
            {
                UserName = employee.User.Email,
                Email = employee.User.Email,
                FullName = employee.User.FullName,
                IsEmployee = true
            };
            var result = await _userManager.CreateAsync(user, "123@Abc"); // default password
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(employee);
            }

            // Thêm role
            await _userManager.AddToRoleAsync(user, role);

            // Thêm Employee
            employee.UserId = user.Id;
            employee.Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper;
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: /Employee/Delete/{id}
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        // POST: /Employee/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                var user = await _userManager.FindByIdAsync(employee.UserId);
                if (user != null)
                    await _userManager.DeleteAsync(user);

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
