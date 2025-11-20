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

            var user = await _userManager.FindByIdAsync(customer.UserId);
            if (user != null) await _userManager.DeleteAsync(user);

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Xóa khách hàng thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}