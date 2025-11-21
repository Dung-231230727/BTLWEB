using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize]
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Trang xem ví của tôi
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // 1. Tìm ví của người dùng hiện tại
            var wallet = await _context.Wallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == user.Id);

            // 2. Nếu chưa có ví thì tự động tạo mới
            if (wallet == null)
            {
                wallet = new Wallet { UserId = user.Id, Balance = 0, LastUpdated = DateTime.Now };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            // 3. Lấy lịch sử giao dịch (Mới nhất lên đầu)
            var transactions = await _context.WalletTransactions
                .Where(t => t.WalletId == wallet.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Truyền danh sách giao dịch sang View qua ViewBag
            ViewBag.Transactions = transactions;

            return View(wallet);
        }
    }
}