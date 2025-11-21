using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; }

        public int WalletId { get; set; }
        public Wallet? Wallet { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Số tiền (+ hoặc -)

        public string? Type { get; set; } // "REFUND" (Hoàn tiền), "COD_DEDUCT" (Trừ COD), "DEPOSIT" (Nạp tiền)
        public string? Description { get; set; }

        public int? RelatedOrderId { get; set; } // Gắn với đơn hàng nào (nếu có)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}