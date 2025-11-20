using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class Order
    {
        public int Id { get; set; }

        // Customer
        public int CustomerId { get; set; }
        [ValidateNever]
        public Customer Customer { get; set; } = null!;

        // Employee
        public int? DispatcherId { get; set; }
        public Employee? Dispatcher { get; set; }

        public int? ShipperId { get; set; }
        public Employee? Shipper { get; set; }

        // Area
        public int PickupAreaId { get; set; }
        [ValidateNever]
        public Area PickupArea { get; set; } = null!;

        public int DeliveryAreaId { get; set; }
        [ValidateNever]
        public Area DeliveryArea { get; set; } = null!;

        // Details
        public string TrackingCode { get; set; } = string.Empty;
        [Required]
        public string ReceiverName { get; set; } = string.Empty;   // Người nhận
        [Required]
        public string ReceiverPhone { get; set; } = string.Empty;  // SĐT người nhận
        [Required]
        public string PickupAddress { get; set; } = string.Empty;
        [Required]
        public string DeliveryAddress { get; set; } = string.Empty;
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DistanceKm { get; set; } = 0;
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal WeightKg { get; set; } = 0;
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; } = 0;

        //payment
        public PayerType Payer { get; set; } = PayerType.Sender;
        public string PaymentMethod { get; set; } = "COD"; // COD, Online
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public string? PaymentTransactionId { get; set; } = null;

        //kho
        public int? PickupWarehouseId { get; set; }
        [ValidateNever]
        public Warehouse? PickupWarehouse { get; set; }

        public int? DeliveryWarehouseId { get; set; }
        [ValidateNever]
        public Warehouse? DeliveryWarehouse { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        //ghi chú
        public string? FailureReason { get; set; }

        // Navigation
        public ICollection<OrderLog>? OrderLogs { get; set; }
    }
}
