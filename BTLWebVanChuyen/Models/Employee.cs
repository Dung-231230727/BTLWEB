namespace BTLWebVanChuyen.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public EmployeeRole Role { get; set; }

        // Navigation
        public ICollection<Order>? OrdersAsDispatcher { get; set; }
        public ICollection<Order>? OrdersAsShipper { get; set; }
    }

    public enum EmployeeRole
    {
        Dispatcher = 1,
        Shipper = 2
    }
}
