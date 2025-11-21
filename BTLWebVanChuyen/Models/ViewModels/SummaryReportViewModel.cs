namespace BTLWebVanChuyen.Models.ViewModels
{
    public class SummaryReportViewModel
    {
        public int TotalOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int FailedOrders { get; set; }
        public decimal TotalRevenue { get; set; }

        // KPI bổ sung
        public int CODOrders { get; set; }
        public int OnlineOrders { get; set; }
        public int PaidOrders { get; set; }
        public int UnpaidOrders { get; set; }
        public int PendingOrders { get; set; }
        public int InTransitOrders { get; set; }
        public decimal AvgWeight { get; set; }
        public decimal AvgDistance { get; set; }

        // % thành công / thất bại
        public double SuccessRate { get; set; }
        public double FailRate { get; set; }
    }
}
