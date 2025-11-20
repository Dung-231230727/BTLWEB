using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.Models
{
    public enum OrderStatus
    {
        // ===== GIAI ĐOẠN KHỞI TẠO =====
        [Display(Name = "Đang chờ xử lý")]
        Pending = 1,

        // ===== GIAO CHO SHIPPER KHU VỰC LẤY =====
        [Display(Name = "Đã giao cho shipper (khu vực lấy)")]
        AssignedPickupShipper = 2,

        [Display(Name = "Đang lấy hàng")]
        Picking = 3,

        [Display(Name = "Lấy hàng thành công (đã về kho khu vực lấy)")]
        PickupSuccess = 4,

        [Display(Name = "Lấy hàng thất bại")]
        PickupFailed = 5,

        // ===== DI CHUYỂN LIÊN KHU VỰC =====
        [Display(Name = "Đang vận chuyển liên khu vực")]
        InterAreaTransporting = 6,

        [Display(Name = "Đã đến kho khu vực giao")]
        ArrivedDeliveryHub = 7,

        // ===== GIAO CHO SHIPPER KHU VỰC GIAO =====
        [Display(Name = "Đã giao cho shipper (khu vực giao)")]
        AssignedDeliveryShipper = 8,

        // ===== GIAO HÀNG =====
        [Display(Name = "Đang giao hàng")]
        Delivering = 9,

        [Display(Name = "Giao thành công")]
        Delivered = 10,

        [Display(Name = "Giao thất bại (đã về kho giao)")]
        DeliveryFailed = 11,

        // ===== HOÀN TRẢ =====
        [Display(Name = "Đang hoàn trả về kho lấy")]
        Returning = 12,

        [Display(Name = "Trả hàng thành công")]
        Returned = 13,

        [Display(Name = "Trả hàng thất bại")]
        ReturnFailed = 14,

        // ===== HỦY =====
        [Display(Name = "Đã hủy")]
        Cancelled = 15
    }
}
