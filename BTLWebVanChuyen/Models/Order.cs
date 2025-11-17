namespace BTLWebVanChuyen.Models
{
    public class Order
    {
        public int Id { get; set; }

        // Customer
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        // Employee
        public int DispatcherId { get; set; }
        public Employee? Dispatcher { get; set; }

        public int ShipperId { get; set; }
        public Employee? Shipper { get; set; }

        // Area
        public int PickupAreaId { get; set; }
        public Area PickupArea { get; set; } = null!;

        public int DeliveryAreaId { get; set; }
        public Area DeliveryArea { get; set; } = null!;

        // Details
        public string TrackingCode { get; set; } = string.Empty;
        public string PickupAddress { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; } = 0;
        public decimal WeightKg { get; set; } = 0;
        public decimal TotalPrice { get; set; } = 0;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<OrderLog>? OrderLogs { get; set; }
    }
}
