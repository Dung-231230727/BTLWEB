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

        // INDEX: Tìm kiếm
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.Customers.Include(c => c.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c => c.User!.FullName.Contains(searchString) || c.User.Email.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // Edit: Thường ít sửa thông tin khách từ Admin, nhưng có thể thêm nếu cần
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                                         .Include(c => c.User)
                                         .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer model)
        {
            if (id != model.Id) return NotFound();

            var customer = await _context.Customers
                                         .Include(c => c.User)
                                         .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Cập nhật thông tin User
            customer.User.FullName = model.User.FullName;
            customer.User.PhoneNumber = model.User.PhoneNumber;

            // Cập nhật thông tin Customer
            customer.Address = model.Address;

            // Lưu User trước
            await _userManager.UpdateAsync(customer.User);

            // Lưu Customer
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            // Thông báo cho admin
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in adminUsers)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Message = $"Khách hàng '{customer.User.FullName}' đã được cập nhật thông tin.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            
            // Thông báo cho customer bị ảnh hưởng
            _context.Notifications.Add(new Notification
            {
                UserId = customer.UserId,
                Message = $"Thông tin tài khoản của bạn đã được cập nhật bởi quản trị viên.",
                CreatedAt = DateTime.Now,
                IsRead = false
            });
            
            await _context.SaveChangesAsync();

            TempData["Message"] = "Cập nhật khách hàng thành công!";
            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // DELETE: Check đơn hàng
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            // Check: Khách đã đặt đơn nào chưa?
            bool hasOrders = await _context.Orders.AnyAsync(o => o.CustomerId == id);
            if (hasOrders)
            {
                TempData["Error"] = "Không thể xóa khách hàng này vì đã có lịch sử đặt hàng.";
                return RedirectToAction(nameof(Index));
            }

            var customerName = customer.User?.FullName ?? "Khách hàng";
            var customerUserId = customer.UserId;
            
            // Thông báo cho customer trước khi xóa
            _context.Notifications.Add(new Notification
            {
                UserId = customerUserId,
                Message = $"Tài khoản của bạn đã bị xóa khỏi hệ thống bởi quản trị viên.",
                CreatedAt = DateTime.Now,
                IsRead = false
            });
            
            // Thông báo cho admin
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in adminUsers)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Message = $"Khách hàng '{customerName}' đã bị xóa khỏi hệ thống.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            
            await _context.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(customer.UserId);
            if (user != null) await _userManager.DeleteAsync(user);

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Xóa khách hàng thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}