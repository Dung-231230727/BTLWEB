namespace BTLWebVanChuyen.Models.ViewModels
{
    public class OrderViewModel
    {
        public Order Order { get; set; } = new Order();

        // Dùng để hiển thị danh sách các khu vực trong dropdown
        public List<Area> pickupAreas { get; set; } = new List<Area>();
        public List<Area> deliveryAreas { get; set; } = new List<Area>();

        // Thêm danh sách giá vận chuyển
        public List<PriceTableViewModel> PriceTables { get; set; } = new List<PriceTableViewModel>();
    }

    public class PriceTableViewModel
    {
        public int AreaId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal PricePerKm { get; set; }
        public decimal WeightPrice { get; set; }
    }
}
