using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; } = null!;

        public EmployeeRole Role { get; set; }

        public int? AreaId { get; set; }
        public Area? Area { get; set; } = null!;

        // Navigation
        public ICollection<Order> OrdersAsDispatcher { get; set; } = new List<Order>();
        public ICollection<Order> OrdersAsShipper { get; set; } = new List<Order>();
    }

    public enum EmployeeRole
    {
        [Display(Name = "Nhân viên điều phối")]
        Dispatcher = 1,
        [Display(Name = "Nhân viên giao hàng")]
        Shipper = 2
    }
}
