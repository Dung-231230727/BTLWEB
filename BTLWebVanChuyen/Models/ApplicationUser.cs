using Microsoft.AspNetCore.Identity;

namespace BTLWebVanChuyen.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public bool IsCustomer { get; set; } = true;
        public bool IsEmployee { get; set; } = false;

        // Navigation
        public Customer? Customer { get; set; }
        public Employee? Employee { get; set; }
    }
}
