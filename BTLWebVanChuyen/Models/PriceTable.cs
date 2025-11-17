namespace BTLWebVanChuyen.Models
{
    public class PriceTable
    {
        public int Id { get; set; }

        public int AreaId { get; set; }
        public Area Area { get; set; } = null!;

        public decimal BasePrice { get; set; }
        public decimal PricePerKm { get; set; }
        public decimal WeightPrice { get; set; }
    }
}
