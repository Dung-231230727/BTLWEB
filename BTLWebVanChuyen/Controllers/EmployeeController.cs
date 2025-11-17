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

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees.Include(e => e.User).ToListAsync();
            return View(employees);
        }

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, string role)
        {
            if (!ModelState.IsValid) return View(employee);

            var user = new ApplicationUser
            {
                UserName = employee.User.Email,
                Email = employee.User.Email,
                FullName = employee.User.FullName,
                IsEmployee = true
            };

            var result = await _userManager.CreateAsync(user, "123@Abc"); // password mặc định
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(employee);
            }

            await _userManager.AddToRoleAsync(user, role);
            employee.UserId = user.Id;
            employee.Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper;
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee employee, string role)
        {
            if (id != employee.Id) return NotFound();
            if (!ModelState.IsValid) return View(employee);

            var emp = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            emp.User.FullName = employee.User.FullName;
            emp.User.Email = employee.User.Email;
            emp.Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper;
            _context.Update(emp);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp != null)
            {
                var user = await _userManager.FindByIdAsync(emp.UserId);
                if (user != null) await _userManager.DeleteAsync(user);

                _context.Employees.Remove(emp);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
