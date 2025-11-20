using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Danh sách thông báo của user hiện tại
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return View(notifications);
        }

        // Đánh dấu 1 thông báo đã đọc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var noti = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == user.Id);

            if (noti == null) return NotFound();

            noti.IsRead = true;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Đánh dấu 1 thông báo là chưa đọc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsUnread(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var noti = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == user.Id);

            if (noti == null) return NotFound();

            noti.IsRead = false;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Đánh dấu tất cả đã đọc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var notis = await _context.Notifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ToListAsync();

            foreach (var n in notis)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
