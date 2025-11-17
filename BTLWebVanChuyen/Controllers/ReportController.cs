using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Trang chính chọn báo cáo
        public IActionResult Index()
        {
            return View();
        }

        // Báo cáo tổng hợp theo ngày
        public async Task<IActionResult> DailyReport(DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Orders.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt.Date <= toDate.Value.Date);

            var dailyData = await query
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailyReport
                {
                    ReportDate = g.Key,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.Status == OrderStatus.Delivered),
                    FailedOrders = g.Count(o => o.Status == OrderStatus.Cancelled),
                    TotalRevenue = g.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalPrice)
                })
                .OrderBy(r => r.ReportDate)
                .ToListAsync();

            return View(dailyData);
        }

        // Báo cáo tổng hợp theo khu vực
        public async Task<IActionResult> AreaReport()
        {
            var data = await _context.Orders
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .GroupBy(o => o.PickupArea.AreaName)
                .Select(g => new
                {
                    AreaName = g.Key,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.Status == OrderStatus.Delivered),
                    FailedOrders = g.Count(o => o.Status == OrderStatus.Cancelled),
                    TotalRevenue = g.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalPrice)
                })
                .ToListAsync();

            return View(data); // View sẽ hiển thị theo bảng
        }

        // Báo cáo tổng quan: tổng đơn, doanh thu, đơn thất bại
        public async Task<IActionResult> SummaryReport()
        {
            var totalOrders = await _context.Orders.CountAsync();
            var deliveredOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Delivered);
            var failedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled);
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.TotalPrice);

            var summary = new DailyReport
            {
                ReportDate = DateTime.Now,
                TotalOrders = totalOrders,
                DeliveredOrders = deliveredOrders,
                FailedOrders = failedOrders,
                TotalRevenue = totalRevenue
            };

            return View(summary);
        }
    }
}
