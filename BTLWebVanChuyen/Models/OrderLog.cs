namespace BTLWebVanChuyen.Models
{
    public class OrderLog
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public OrderStatus Status { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;

        public string? Note { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
