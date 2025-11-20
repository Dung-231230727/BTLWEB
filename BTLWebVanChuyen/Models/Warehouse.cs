using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTLWebVanChuyen.Models
{
    public class Warehouse
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên kho")]
        [Display(Name = "Tên kho hàng")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ kho")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;

        // Liên kết với Khu vực
        [Display(Name = "Thuộc khu vực")]
        public int AreaId { get; set; }

        [ForeignKey("AreaId")]
        public Area? Area { get; set; }
    }
}
