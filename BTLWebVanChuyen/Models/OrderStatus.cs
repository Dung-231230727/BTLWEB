using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.Models
{
    public enum OrderStatus
    {
        [Display(Name = "Chờ gán bởi nhân viên điều phối")]
        Pending = 1,        // Chờ gán nhân viên điều phối
        [Display(Name = "Đã giao cho nhân viên giao hàng")]
        Assigned = 2,       // Dispatcher đã gán shipper
        [Display(Name = "Đang giao hàng")]
        Delivering = 3,     // Shipper đang giao
        [Display(Name = "Giao thành công")]
        Delivered = 4,      // Giao thành công
        [Display(Name = "Giao thất bại")]
        Failed = 5,         // Giao thất bại
        [Display(Name = "Đã hủy")]
        Cancelled = 6
    }
}
