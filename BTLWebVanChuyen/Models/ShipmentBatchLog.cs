using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class ShipmentBatchLog
    {
        [Key]
        public int Id { get; set; }

        public int ShipmentBatchId { get; set; }
        public ShipmentBatch? ShipmentBatch { get; set; }

        public ShipmentBatchStatus Status { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public string? Note { get; set; }
        public string? UpdatedBy { get; set; } // Lưu email/username người thao tác
    }
}