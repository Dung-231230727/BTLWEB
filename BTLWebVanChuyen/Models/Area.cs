namespace BTLWebVanChuyen.Models
{
    public class Area
    {
        public int AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string AreaCode { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Orders pickup/delivery
        public ICollection<Order>? PickupOrders { get; set; }
        public ICollection<Order>? DeliveryOrders { get; set; }

        // Price
        public ICollection<PriceTable>? PriceTables { get; set; }
    }
}
