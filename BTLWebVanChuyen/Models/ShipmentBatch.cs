using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class ShipmentBatch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? BatchCode { get; set; } // VD: LOHANG_20251122_01

        // Kho Xuất (Nơi tạo lô)
        public int OriginWarehouseId { get; set; }
        public Warehouse? OriginWarehouse { get; set; } = null!;

        // Kho Nhập (Nơi nhận lô)
        public int DestinationWarehouseId { get; set; }
        public Warehouse? DestinationWarehouse { get; set; } = null!;

        // Tài xế xe tải (Linehaul Driver) - Có thể null nếu thuê xe ngoài
        public int? ShipperId { get; set; }
        public Employee? Shipper { get; set; }

        public ShipmentBatchStatus Status { get; set; } = ShipmentBatchStatus.Created;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        // Danh sách đơn trong lô
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<ShipmentBatchLog> BatchLogs { get; set; } = new List<ShipmentBatchLog>();
    }
}