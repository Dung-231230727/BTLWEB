using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BTLWebVanChuyen.Models
{
    public class PriceTable
    {
        public int Id { get; set; }

        public int AreaId { get; set; }

        [ValidateNever]
        public Area? Area { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal BasePrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PricePerKm { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal WeightPrice { get; set; }
    }
}
