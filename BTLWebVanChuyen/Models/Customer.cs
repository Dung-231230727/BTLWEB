namespace BTLWebVanChuyen.Models
{
    public class Customer
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; } = null!;

        public string Address { get; set; } = string.Empty;
        public string? CompanyName { get; set; }

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
