using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    // 1. Authorize chung cho cả Admin và Customer (để vào được Info)
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ============================================================
        // PHẦN 1: DÀNH CHO ADMIN (QUẢN LÝ DANH SÁCH)
        // ============================================================

        // INDEX: Chỉ Admin được xem danh sách
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.Customers.Include(c => c.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c => c.User!.FullName.Contains(searchString) || c.User.Email!.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await query.ToListAsync());
        }

        // DETAILS: Chỉ Admin được xem chi tiết người khác
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // DELETE: Chỉ Admin được xóa
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var customer = await _context.Customers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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

        // ============================================================
        // PHẦN 2: DÀNH CHO KHÁCH HÀNG (PROFILE CÁ NHÂN)
        // ============================================================

        // GET: /Customer/Info
        [HttpGet]
        public async Task<IActionResult> Info()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

            var userInDb = await _userManager.FindByIdAsync(userId);
            var customerInDb = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (customerInDb == null) return NotFound();

            // Gán user vào customer để View hiển thị
            customerInDb.User = userInDb;

            // QUAN TRỌNG: Không gán TempData["Success"] ở đây
            return View(customerInDb);
        }

        // POST: /Customer/Info
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Info(Customer model, string? currentPassword, string? newPassword, string? confirmPassword)
        {
            var userId = _userManager.GetUserId(User);
            var userInDb = await _userManager.FindByIdAsync(userId!);
            var customerInDb = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

            if (userInDb == null || customerInDb == null) return NotFound();

            // 1. Bỏ qua lỗi validate Address (cho phép để trống)
            ModelState.Remove("Address");

            // Nếu có lỗi khác (vd sai định dạng email/sđt), trả về View ngay
            if (!ModelState.IsValid)
            {
                customerInDb.User = userInDb;
                return View(customerInDb);
            }

            bool hasError = false;

            // 2. Cập nhật thông tin cơ bản
            // Sử dụng ?? "" để nếu null thì lưu chuỗi rỗng, tránh lỗi DB
            customerInDb.Address = model.Address ?? "";
            _context.Customers.Update(customerInDb);

            if (model.User != null)
            {
                userInDb.FullName = model.User.FullName;
                userInDb.PhoneNumber = model.User.PhoneNumber;

                // Logic check trùng Email (nếu có thay đổi)
                if (!string.Equals(userInDb.Email, model.User.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var emailExists = await _userManager.FindByEmailAsync(model.User.Email!);
                    if (emailExists != null && emailExists.Id != userId)
                    {
                        TempData["Error"] = "Email này đã được sử dụng bởi tài khoản khác.";
                        hasError = true;
                    }
                    else
                    {
                        userInDb.Email = model.User.Email;
                        userInDb.UserName = model.User.Email;
                    }
                }

                if (!hasError)
                {
                    var updateResult = await _userManager.UpdateAsync(userInDb);
                    if (!updateResult.Succeeded)
                    {
                        TempData["Error"] = "Lỗi cập nhật User: " + updateResult.Errors.FirstOrDefault()?.Description;
                        hasError = true;
                    }
                }
            }

            // 3. Xử lý Đổi Mật Khẩu (Chỉ chạy khi người dùng nhập dữ liệu vào ô mật khẩu)
            bool wantsToChangePassword = !string.IsNullOrEmpty(currentPassword)
                                      || !string.IsNullOrEmpty(newPassword)
                                      || !string.IsNullOrEmpty(confirmPassword);

            if (wantsToChangePassword && !hasError)
            {
                // Nếu muốn đổi mật khẩu, bắt buộc nhập mật khẩu hiện tại
                if (string.IsNullOrEmpty(currentPassword))
                {
                    TempData["Error"] = "Vui lòng nhập mật khẩu hiện tại.";
                    hasError = true;
                }
                // Nếu nhập mật khẩu mới hoặc xác nhận, bắt buộc phải điền đầy đủ
                else if (string.IsNullOrEmpty(newPassword) && !string.IsNullOrEmpty(confirmPassword))
                {
                    TempData["Error"] = "Vui lòng nhập mật khẩu mới.";
                    hasError = true;
                }
                else if (!string.IsNullOrEmpty(newPassword) && newPassword != confirmPassword)
                {
                    TempData["Error"] = "Mật khẩu xác nhận không khớp.";
                    hasError = true;
                }
                else if (!string.IsNullOrEmpty(newPassword))
                {
                    var changePassResult = await _userManager.ChangePasswordAsync(userInDb, currentPassword, newPassword);
                    if (!changePassResult.Succeeded)
                    {
                        TempData["Error"] = "Đổi mật khẩu thất bại: " + changePassResult.Errors.FirstOrDefault()?.Description;
                        hasError = true;
                    }
                }
            }

            // 4. Kết thúc
            if (!hasError)
            {
                await _context.SaveChangesAsync();

                // CHỈ GÁN THÔNG BÁO THÀNH CÔNG TẠI ĐÂY
                TempData["Success"] = "Cập nhật thông tin thành công!";

                // QUAN TRỌNG: Redirect về trang Info (GET) để xóa TempData sau khi hiện Alert
                // Giúp F5 không bị hiện lại thông báo
                return RedirectToAction("Info");
            }

            // Nếu có lỗi, giữ nguyên trang để hiển thị lỗi
            customerInDb.User = userInDb;
            return View(customerInDb);
        }
    }
}