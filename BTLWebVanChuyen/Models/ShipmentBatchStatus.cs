using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.Models
{
    public enum ShipmentBatchStatus
    {
        [Display(Name = "Mới tạo")]
        Created = 1,      // Vừa gom đơn, chưa đi

        [Display(Name = "Đang vận chuyển")]
        InTransit = 2,    // Xe đã rời kho xuất

        [Display(Name = "Đã đến kho đích")]
        Completed = 3     // Xe đã đến, chuẩn bị rã lô
    }
}