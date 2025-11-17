using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using BTLWebVanChuyen.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ============================
        // ADMIN + DISPATCHER: Danh sách đơn
        // ============================
        [Authorize(Roles = "Admin,Dispatcher")]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.Customer).ThenInclude(c => c.User)
                .Include(o => o.Dispatcher).ThenInclude(d => d.User)
                .Include(o => o.Shipper).ThenInclude(s => s.User)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .ToListAsync();

            if (User.IsInRole("Dispatcher"))
            {
                ViewBag.Shippers = _context.Employees
                    .Where(e => e.Role == EmployeeRole.Shipper)
                    .Include(e => e.User)
                    .ToList();
            }

            return View(orders);
        }

        // ============================
        // Customer: Create
        // ============================
        [Authorize(Roles = "Customer")]
        public IActionResult Create()
        {
            var vm = new OrderViewModel
            {
                Order = new Order(),
                pickupAreas = _context.Areas.ToList(),
                deliveryAreas = _context.Areas.ToList(),
                PriceTables = _context.PriceTables
                                .Select(p => new PriceTableViewModel
                                {
                                    AreaId = p.AreaId,
                                    BasePrice = p.BasePrice,
                                    PricePerKm = p.PricePerKm,
                                    WeightPrice = p.WeightPrice
                                })
                                .ToList()
            };
            return View(vm);
        }

        [HttpPost, Authorize(Roles = "Customer"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.pickupAreas = _context.Areas.ToList();
                vm.deliveryAreas = _context.Areas.ToList();
                vm.PriceTables = _context.PriceTables
                                .Select(p => new PriceTableViewModel
                                {
                                    AreaId = p.AreaId,
                                    BasePrice = p.BasePrice,
                                    PricePerKm = p.PricePerKm,
                                    WeightPrice = p.WeightPrice
                                })
                                .ToList();
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (customer == null)
            {
                ModelState.AddModelError("", "Không tìm thấy thông tin khách hàng.");
                return View(vm);
            }

            var order = vm.Order;
            order.CustomerId = customer.Id;
            order.CreatedAt = DateTime.Now;
            order.Status = OrderStatus.Pending;

            // Tính lại TotalPrice trên server
            var pickupPrice = await _context.PriceTables.FirstOrDefaultAsync(p => p.AreaId == order.PickupAreaId);
            var deliveryPrice = await _context.PriceTables.FirstOrDefaultAsync(p => p.AreaId == order.DeliveryAreaId);
            decimal totalPrice = 0;
            if (pickupPrice != null && deliveryPrice != null)
            {
                totalPrice = pickupPrice.BasePrice + deliveryPrice.BasePrice
                            + order.DistanceKm * (pickupPrice.PricePerKm + deliveryPrice.PricePerKm)
                            + order.WeightKg * (pickupPrice.WeightPrice + deliveryPrice.WeightPrice);
            }
            order.TotalPrice = totalPrice;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Sinh tracking code
            order.TrackingCode = $"MVD{order.CreatedAt:ddMMyyyy}{order.Id:D4}";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = order.Id });
        }

        // ============================
        // CUSTOMER: xem đơn hàng của mình
        // ============================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);

            var orders = await _context.Orders
                .Where(o => o.CustomerId == customer.Id)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .Include(o => o.Dispatcher)
                .Include(o => o.Shipper)
                .ToListAsync();

            return View(orders);
        }

        // ============================
        // Xem chi tiết đơn hàng
        // ============================
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer).ThenInclude(c => c.User)
                .Include(o => o.Dispatcher).ThenInclude(d => d.User)
                .Include(o => o.Shipper).ThenInclude(s => s.User)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .Include(o => o.OrderLogs)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (User.IsInRole("Dispatcher"))
            {
                ViewBag.Shippers = _context.Employees
                    .Where(e => e.Role == EmployeeRole.Shipper)
                    .Include(e => e.User)
                    .ToList();
            }

            return View(order);
        }

        // ============================
        // DISPATCHER: Gán shipper
        // ============================
        [HttpPost, Authorize(Roles = "Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int orderId, int shipperId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var shipper = await _context.Employees.FindAsync(shipperId);

            if (order == null || shipper == null) return NotFound();

            order.ShipperId = shipperId;
            order.Status = OrderStatus.Assigned;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ============================
        // SHIPPER: Cập nhật trạng thái đơn
        // ============================
        [HttpPost, Authorize(Roles = "Shipper"), ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            order.Status = status;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ============================
        // Admin/Dispatcher: Edit
        // ============================
        [Authorize(Roles = "Admin,Dispatcher")]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var vm = new OrderViewModel
            {
                Order = order,
                pickupAreas = _context.Areas.ToList(),
                deliveryAreas = _context.Areas.ToList(),
                PriceTables = _context.PriceTables
                                .Select(p => new PriceTableViewModel
                                {
                                    AreaId = p.AreaId,
                                    BasePrice = p.BasePrice,
                                    PricePerKm = p.PricePerKm,
                                    WeightPrice = p.WeightPrice
                                })
                                .ToList()
            };

            return View(vm);
        }

        [HttpPost, Authorize(Roles = "Admin,Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrderViewModel vm)
        {
            if (id != vm.Order.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.pickupAreas = _context.Areas.ToList();
                vm.deliveryAreas = _context.Areas.ToList();
                vm.PriceTables = _context.PriceTables
                                .Select(p => new PriceTableViewModel
                                {
                                    AreaId = p.AreaId,
                                    BasePrice = p.BasePrice,
                                    PricePerKm = p.PricePerKm,
                                    WeightPrice = p.WeightPrice
                                })
                                .ToList();
                return View(vm);
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.PickupAreaId = vm.Order.PickupAreaId;
            order.DeliveryAreaId = vm.Order.DeliveryAreaId;
            order.PickupAddress = vm.Order.PickupAddress;
            order.DeliveryAddress = vm.Order.DeliveryAddress;
            order.DistanceKm = vm.Order.DistanceKm;
            order.WeightKg = vm.Order.WeightKg;
            order.Status = vm.Order.Status;

            // Tính lại TotalPrice
            var pickupPrice = await _context.PriceTables.FirstOrDefaultAsync(p => p.AreaId == order.PickupAreaId);
            var deliveryPrice = await _context.PriceTables.FirstOrDefaultAsync(p => p.AreaId == order.DeliveryAreaId);
            decimal totalPrice = 0;
            if (pickupPrice != null && deliveryPrice != null)
            {
                totalPrice = pickupPrice.BasePrice + deliveryPrice.BasePrice
                            + order.DistanceKm * (pickupPrice.PricePerKm + deliveryPrice.PricePerKm)
                            + order.WeightKg * (pickupPrice.WeightPrice + deliveryPrice.WeightPrice);
            }
            order.TotalPrice = totalPrice;

            _context.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = order.Id });
        }


        // ============================
        // DELETE (Admin)
        // ============================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Dispatcher)
                .Include(o => o.Shipper)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost, ActionName("Delete"), Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order != null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================
        // Tracking đơn hàng
        // ============================
        [AllowAnonymous]
        public IActionResult Tracking() => View();

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Tracking(string trackingCode)
        {
            if (string.IsNullOrWhiteSpace(trackingCode))
            {
                ViewBag.Error = "Vui lòng nhập mã vận đơn.";
                return View();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Dispatcher)
                .Include(o => o.Shipper)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .Include(o => o.OrderLogs)
                .FirstOrDefaultAsync(o => o.TrackingCode == trackingCode);

            if (order == null)
            {
                ViewBag.Error = $"Không tìm thấy đơn hàng với mã vận đơn {trackingCode}.";
                return View();
            }

            return View("Details", order);
        }
    }
}
