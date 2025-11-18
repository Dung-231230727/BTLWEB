namespace BTLWebVanChuyen.Models.ViewModels
{
    public class ReportViewModel
    {
        // Dùng cho báo cáo theo ngày hoặc tổng quan
        public DateTime? ReportDate { get; set; } // Nullable, vì báo cáo tổng quan không cần ngày

        // Dùng cho báo cáo theo khu vực
        public string? AreaName { get; set; } // Nullable, vì báo cáo theo ngày không cần

        public int TotalOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int FailedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
