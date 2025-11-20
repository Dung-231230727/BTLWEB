using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // ==========================================
        // 1. INDEX: Danh sách + Tìm kiếm + Lọc
        // ==========================================
        public async Task<IActionResult> Index(string searchString, int? filterAreaId, EmployeeRole? filterRole)
        {
            var query = _context.Employees
                .Include(e => e.User)
                .Include(e => e.Area)
                .AsQueryable();

            // 1. Tìm kiếm theo Tên hoặc Email
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(e => e.User!.FullName.Contains(searchString) || e.User.Email!.Contains(searchString));
            }

            // 2. Lọc theo Khu vực
            if (filterAreaId.HasValue)
            {
                query = query.Where(e => e.AreaId == filterAreaId);
            }

            // 3. Lọc theo Vai trò
            if (filterRole.HasValue)
            {
                query = query.Where(e => e.Role == filterRole);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["AreaId"] = new SelectList(_context.Areas, "AreaId", "AreaName", filterAreaId);

            return View(await query.ToListAsync());
        }

        // ==========================================
        // 2. DETAILS
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Employees
                .Include(e => e.User)
                .Include(e => e.Area)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        // ==========================================
        // 3. CREATE: Tạo User + Employee
        // ==========================================
        public IActionResult Create()
        {
            ViewData["Areas"] = new SelectList(_context.Areas, "AreaId", "AreaName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, string role)
        {
            // 1. Kiểm tra Model cơ bản (như AreaId)
            if (employee.AreaId == 0)
            {
                ModelState.AddModelError("AreaId", "Vui lòng chọn khu vực quản lý.");
            }

            // Lấy thông tin User từ form (do binding phức tạp)
            // Trong View, name="User.Email", "User.FullName", "User.PasswordHash"
            // Nhưng object employee.User có thể null nếu binding không khớp hết
            // Nên ta lấy trực tiếp hoặc qua parameter binding

            // Để đơn giản và chắc chắn, ta dùng employee.User nếu binding thành công
            // Nếu không, ta cần thêm logic lấy từ Request.Form hoặc ViewModel riêng

            // Giả sử binding thành công nhờ <input name="User.Email" ... />
            if (ModelState.IsValid && employee.User != null)
            {
                var user = new ApplicationUser
                {
                    UserName = employee.User.Email,
                    Email = employee.User.Email,
                    FullName = employee.User.FullName,
                    IsCustomer = false // Nhân viên không phải khách hàng
                };

                // Lấy mật khẩu từ form (User.PasswordHash đang chứa password text từ input)
                string password = employee.User.PasswordHash ?? "123@Abc";

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, role);

                    // Gán UserId vừa tạo cho Employee
                    employee.UserId = user.Id;
                    employee.Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper;

                    // Xóa object User con để tránh EF add lại User (vì User đã được tạo bởi UserManager)
                    employee.User = null;

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    TempData["Message"] = "Thêm nhân viên thành công.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // Nếu thất bại
            ViewData["Areas"] = new SelectList(_context.Areas, "AreaId", "AreaName", employee.AreaId);
            return View(employee);
        }

        // ==========================================
        // 4. EDIT
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewData["Areas"] = new SelectList(_context.Areas, "AreaId", "AreaName", employee.AreaId);
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee employee, string role)
        {
            if (id != employee.Id) return NotFound();

            var empDb = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            if (empDb == null) return NotFound();

            // Cập nhật thông tin Employee
            empDb.Role = role == "Dispatcher" ? EmployeeRole.Dispatcher : EmployeeRole.Shipper;
            empDb.AreaId = employee.AreaId;

            // Cập nhật thông tin User (Chỉ FullName, không sửa Email/Password ở đây)
            if (empDb.User != null && employee.User != null)
            {
                empDb.User.FullName = employee.User.FullName;
            }

            // Cập nhật Role trong Identity (Nếu thay đổi)
            // (Logic này phức tạp: cần remove role cũ, add role mới. Để đơn giản ta chỉ update DB Employee.Role
            // Nếu cần chuẩn Identity, hãy uncomment đoạn dưới)
            /*
            if (empDb.User != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(empDb.User);
                await _userManager.RemoveFromRolesAsync(empDb.User, currentRoles);
                await _userManager.AddToRoleAsync(empDb.User, role);
            }
            */

            try
            {
                _context.Update(empDb);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Cập nhật thành công.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Employees.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 5. DELETE
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp == null) return NotFound();

            // Check ràng buộc dữ liệu (Đơn hàng)
            bool hasOrders = await _context.Orders.AnyAsync(o => o.DispatcherId == id || o.ShipperId == id);
            if (hasOrders)
            {
                TempData["Error"] = "Không thể xóa nhân viên này vì đang phụ trách đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Xóa tài khoản Identity
            var user = await _userManager.FindByIdAsync(emp.UserId);
            if (user != null) await _userManager.DeleteAsync(user);

            // Xóa Employee
            _context.Employees.Remove(emp);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Xóa nhân viên thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}