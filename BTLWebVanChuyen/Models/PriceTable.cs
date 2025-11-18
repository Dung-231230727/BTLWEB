using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class PriceTable
    {
        public int Id { get; set; }

        public int AreaId { get; set; }
        public Area Area { get; set; } = null!;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal BasePrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PricePerKm { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal WeightPrice { get; set; }
    }
}
