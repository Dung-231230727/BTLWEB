using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.Models
{
    // === Trạng thái thanh toán chi tiết ===
    public enum PaymentStatus
    {
        [Display(Name = "Chưa thanh toán")]
        Unpaid = 1,
        [Display(Name = "Đã thanh toán")]
        Paid = 2,
        [Display(Name = "Đang chờ thanh toán Online")]
        ProcessingOnline = 3
    }

    // === Người trả phí vận chuyển ===
    public enum PayerType
    {
        [Display(Name = "Người gửi trả")]
        Sender = 1,
        [Display(Name = "Người nhận trả")]
        Receiver = 2
    }
}
