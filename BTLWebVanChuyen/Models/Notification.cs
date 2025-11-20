using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ===== Foreign keys =====

        // User nhận thông báo (ApplicationUser.Id)
        [Required]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        // Đơn hàng liên quan (có thể null nếu là thông báo hệ thống khác)
        [Column("order_id")]
        public int? OrderId { get; set; }

        public Order? Order { get; set; }
    }
}
