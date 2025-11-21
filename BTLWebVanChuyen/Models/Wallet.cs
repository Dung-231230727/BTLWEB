using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class Wallet
    {
        [Key]
        public int Id { get; set; }

        // Liên kết 1-1 với User (Cả Customer và Employee đều có ví)
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0; // Số dư

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}