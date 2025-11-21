using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using BTLWebVanChuyen.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        public IActionResult Index()
        {
            return View();
        }

        // Báo cáo theo ngày
        public async Task<IActionResult> DailyReport(DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            var query = _context.Orders.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt.Date <= toDate.Value.Date);

            var dailyData = await query
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailyReportViewModel
                {
                    ReportDate = g.Key,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.Status == OrderStatus.Delivered),
                    FailedOrders = g.Count(o => o.Status == OrderStatus.Cancelled),
                    TotalRevenue = g.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalPrice),
                    CODOrders = g.Count(o => o.PaymentMethod == "COD"),
                    OnlineOrders = g.Count(o => o.PaymentMethod == "Online")
                })
                .OrderBy(r => r.ReportDate)
                .ToListAsync();

            return View(dailyData);
        }


        // Báo cáo theo khu vực
        public async Task<IActionResult> AreaReport()
        {
            var data = await _context.Orders
                .Include(o => o.PickupArea)
                .GroupBy(o => o.PickupArea.AreaName)
                .Select(g => new AreaReportViewModel
                {
                    AreaName = g.Key,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.Status == OrderStatus.Delivered),
                    FailedOrders = g.Count(o => o.Status == OrderStatus.Cancelled),
                    CODOrders = g.Count(o => o.PaymentMethod == "COD"),
                    OnlineOrders = g.Count(o => o.PaymentMethod == "Online"),
                    TotalRevenue = g.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalPrice)
                })
                .ToListAsync();

            return View(data);
        }

        // Báo cáo tổng quan
        public async Task<IActionResult> SummaryReport()
        {
            var orders = await _context.Orders.ToListAsync();
            var totalOrders = orders.Count;
            var deliveredOrders = orders.Count(o => o.Status == OrderStatus.Delivered);
            var failedOrders = orders.Count(o => o.Status == OrderStatus.Cancelled);

            var summary = new SummaryReportViewModel
            {
                TotalOrders = totalOrders,
                DeliveredOrders = deliveredOrders,
                FailedOrders = failedOrders,
                TotalRevenue = orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalPrice),

                CODOrders = orders.Count(o => o.PaymentMethod == "COD"),
                OnlineOrders = orders.Count(o => o.PaymentMethod == "Online"),
                PaidOrders = orders.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                UnpaidOrders = orders.Count(o => o.PaymentStatus == PaymentStatus.Unpaid),
                PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                AvgWeight = orders.Any() ? (decimal)orders.Average(o => o.WeightKg) : 0,
                AvgDistance = orders.Any() ? (decimal)orders.Average(o => o.DistanceKm) : 0,

                SuccessRate = totalOrders > 0 ? deliveredOrders * 100.0 / totalOrders : 0,
                FailRate = totalOrders > 0 ? failedOrders * 100.0 / totalOrders : 0
            };

            // Nếu muốn chart doanh thu theo ngày, tạo ViewBag
            var dailyGroups = orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key.ToString("dd/MM/yyyy"), Revenue = g.Sum(x => x.TotalPrice) })
                .ToList();

            ViewBag.DailyLabelsJson = JsonSerializer.Serialize(dailyGroups.Select(g => g.Date));
            ViewBag.DailyRevenueJson = JsonSerializer.Serialize(dailyGroups.Select(g => g.Revenue));

            return View(summary);
        }

    }
}
