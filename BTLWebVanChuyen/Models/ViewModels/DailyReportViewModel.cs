namespace BTLWebVanChuyen.Models.ViewModels
{
    public class DailyReportViewModel
    {
        public DateTime? ReportDate { get; set; }
        public int TotalOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int FailedOrders { get; set; }
        public decimal TotalRevenue { get; set; }

        public int CODOrders { get; set; }
        public int OnlineOrders { get; set; }

        public double SuccessRate => TotalOrders == 0 ? 0 : (double)DeliveredOrders / TotalOrders * 100;
        public double FailRate => TotalOrders == 0 ? 0 : (double)FailedOrders / TotalOrders * 100;
    }
}
