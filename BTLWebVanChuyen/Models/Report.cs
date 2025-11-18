namespace BTLWebVanChuyen.Models
{
    public class Report
    {
        public int Id { get; set; }
        public DateTime ReportDate { get; set; }
        public int TotalOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int FailedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
