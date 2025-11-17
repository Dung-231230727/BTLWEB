using BTLWebVanChuyen.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;
    public ReportController(ApplicationDbContext context) => _context = context;

    public IActionResult Index() => View();

    // Ví dụ: thống kê số lượng đơn hàng theo trạng thái
    public async Task<IActionResult> OrdersByStatus()
    {
        var data = await _context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return View(data);
    }
}
