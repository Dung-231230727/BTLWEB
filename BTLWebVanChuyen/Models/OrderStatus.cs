namespace BTLWebVanChuyen.Models
{
    public enum OrderStatus
    {
        Pending = 1,        // Chờ gán nhân viên điều phối
        Assigned = 2,       // Dispatcher đã gán shipper
        Delivering = 3,     // Shipper đang giao
        Delivered = 4,      // Giao thành công
        Failed = 5,         // Giao thất bại
        Cancelled = 6
    }
}
