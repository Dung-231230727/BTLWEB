using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using BTLWebVanChuyen.Models.ViewModels;
using BTLWebVanChuyen.Utility;
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
        private readonly IConfiguration _configuration;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        // ============================
        // ADMIN + DISPATCHER + Shipper: Danh sách đơn
        // ============================
        [Authorize(Roles = "Admin,Dispatcher,Shipper")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            IQueryable<Order> query = _context.Orders
                .Include(o => o.Customer).ThenInclude(c => c.User)
                .Include(o => o.Dispatcher).ThenInclude(d => d!.User)
                .Include(o => o.Shipper).ThenInclude(s => s!.User)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea);

            if (User.IsInRole("Admin"))
            {
                // Admin xem tất cả
            }
            else if (User.IsInRole("Dispatcher"))
            {
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id && e.Role == EmployeeRole.Dispatcher);
                if (dispatcher == null) return Forbid();
                query = query.Where(o => o.PickupAreaId == dispatcher.AreaId || o.DeliveryAreaId == dispatcher.AreaId);
            }
            else if (User.IsInRole("Shipper"))
            {
                var shipper = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id && e.Role == EmployeeRole.Shipper);
                if (shipper == null) return Forbid();
                query = query.Where(o => o.ShipperId == shipper.Id);
            }

            ViewBag.Shippers = await _context.Employees.Where(e => e.Role == EmployeeRole.Shipper).Include(e => e.User).ToListAsync();
            return View(await query.OrderByDescending(o => o.CreatedAt).ToListAsync());
        }

        // ============================
        // Details (Xem chi tiết)
        // ============================
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer).ThenInclude(c => c.User)
                .Include(o => o.Dispatcher).ThenInclude(d => d!.User)
                .Include(o => o.Shipper).ThenInclude(s => s!.User)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .Include(o => o.PickupWarehouse)   // Load Kho Lấy
                .Include(o => o.DeliveryWarehouse) // Load Kho Giao
                .Include(o => o.OrderLogs)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (User.IsInRole("Dispatcher"))
            {
                var user = await _userManager.GetUserAsync(User);
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);

                if (dispatcher != null)
                {
                    // 1. Lấy danh sách Shipper thuộc khu vực của Dispatcher
                    ViewBag.Shippers = await _context.Employees
                        .Where(e => e.Role == EmployeeRole.Shipper && e.AreaId == dispatcher.AreaId)
                        .Include(e => e.User)
                        .ToListAsync();

                    // 2. Lấy danh sách Kho thuộc khu vực của Dispatcher (Để chọn khi gán)
                    ViewBag.Warehouses = await _context.Warehouses
                        .Where(w => w.AreaId == dispatcher.AreaId)
                        .ToListAsync();
                }
            }

            return View(order);
        }

        // ============================
        // LOGIC ĐIỀU PHỐI (ASSIGN)
        // ============================
        [HttpPost, Authorize(Roles = "Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int orderId, int shipperId, int? warehouseId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var shipper = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == shipperId);
            var user = await _userManager.GetUserAsync(User);
            var dispatcher = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.UserId == user!.Id);

            if (order == null || shipper == null || dispatcher == null)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            bool isSameArea = order.PickupAreaId == order.DeliveryAreaId;

            // --- CASE 1: GÁN LẤY HÀNG (Tại khu vực lấy) ---
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PickupFailed)
            {
                if (dispatcher.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Bạn không phải Dispatcher khu vực lấy hàng." });
                if (shipper.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Shipper phải thuộc khu vực lấy hàng." });

                if (warehouseId == null)
                    return Json(new { success = false, message = "Vui lòng chọn Kho để nhập hàng sau khi lấy." });

                order.Status = OrderStatus.AssignedPickupShipper;
                order.PickupWarehouseId = warehouseId; // Lưu kho nhập
            }

            // --- CASE 2: GÁN GIAO HÀNG (Tại khu vực giao) ---
            else if (order.Status == OrderStatus.ArrivedDeliveryHub ||
                    (order.Status == OrderStatus.PickupSuccess && isSameArea) ||
                     order.Status == OrderStatus.DeliveryFailed)
            {
                if (dispatcher.AreaId != order.DeliveryAreaId)
                    return Json(new { success = false, message = "Bạn không phải Dispatcher khu vực giao hàng." });
                if (shipper.AreaId != order.DeliveryAreaId)
                    return Json(new { success = false, message = "Shipper phải thuộc khu vực giao hàng." });

                if (warehouseId == null)
                {
                    // Nếu cùng vùng, tự động lấy kho nhập làm kho xuất nếu không chọn
                    if (isSameArea && order.PickupWarehouseId.HasValue)
                        warehouseId = order.PickupWarehouseId;
                    else
                        return Json(new { success = false, message = "Vui lòng chọn Kho xuất hàng để đi giao." });
                }

                order.Status = OrderStatus.AssignedDeliveryShipper;
                order.DeliveryWarehouseId = warehouseId; // Lưu kho xuất
            }
            else
            {
                return Json(new { success = false, message = "Trạng thái đơn hàng không cho phép gán Shipper lúc này." });
            }

            order.ShipperId = shipper.Id;
            order.DispatcherId = dispatcher.Id;

            // Ghi log
            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = order.Status,
                Time = DateTime.Now,
                Note = $"Điều phối cho {shipper.User!.FullName} bởi {dispatcher.User!.FullName}",
                UpdatedBy = dispatcher.User.UserName
            });

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                statusDisplay = order.Status.GetDisplayName(),
                dispatcherName = dispatcher.User?.FullName,
                shipperName = shipper.User?.FullName
            });
        }

        // ============================
        // LOGIC CẬP NHẬT TRẠNG THÁI (UPDATE STATUS)
        // ============================
        [HttpPost, Authorize(Roles = "Shipper,Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus status)
        {
            var order = await _context.Orders
                .Include(o => o.PickupArea).Include(o => o.DeliveryArea)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Returned || order.Status == OrderStatus.Cancelled)
                return Json(new { success = false, message = "Đơn hàng đã kết thúc." });

            bool isSameArea = order.PickupAreaId == order.DeliveryAreaId;
            var user = await _userManager.GetUserAsync(User);
            bool isValidTransition = false;

            // --- SHIPPER ---
            if (User.IsInRole("Shipper"))
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (order.ShipperId != currentEmployee?.Id)
                    return Json(new { success = false, message = "Bạn không phải Shipper phụ trách." });

                // 1. Lấy hàng
                if (order.Status == OrderStatus.AssignedPickupShipper && status == OrderStatus.Picking) isValidTransition = true;
                else if (order.Status == OrderStatus.Picking && (status == OrderStatus.PickupSuccess || status == OrderStatus.PickupFailed)) isValidTransition = true;
                // 2. Giao hàng
                else if (order.Status == OrderStatus.AssignedDeliveryShipper && status == OrderStatus.Delivering) isValidTransition = true;
                else if (order.Status == OrderStatus.Delivering && (status == OrderStatus.Delivered || status == OrderStatus.DeliveryFailed)) isValidTransition = true;
                // 3. Hoàn trả
                else if (order.Status == OrderStatus.Returning && (status == OrderStatus.Returned || status == OrderStatus.ReturnFailed)) isValidTransition = true;
            }

            // --- DISPATCHER ---
            else if (User.IsInRole("Dispatcher"))
            {
                // 1. Luân chuyển hàng
                if (order.Status == OrderStatus.PickupSuccess && !isSameArea && status == OrderStatus.InterAreaTransporting) isValidTransition = true;
                else if (order.Status == OrderStatus.InterAreaTransporting && status == OrderStatus.ArrivedDeliveryHub)
                {
                    order.ShipperId = null; // Xóa Shipper cũ
                    isValidTransition = true;
                }
                // 2. Xử lý sự cố
                else if (status == OrderStatus.Cancelled || status == OrderStatus.Returning) isValidTransition = true;
            }

            if (!isValidTransition) return Json(new { success = false, message = "Chuyển đổi trạng thái không hợp lệ." });

            order.Status = status;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = status,
                Time = DateTime.Now,
                Note = $"Cập nhật trạng thái bởi {user!.UserName}",
                UpdatedBy = user.UserName
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, statusDisplay = order.Status.GetDisplayName() });
        }

        // ============================
        // MarkAsPaid (Thu tiền COD)
        // ============================
        [HttpPost, Authorize(Roles = "Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null && order.PaymentStatus != PaymentStatus.Paid && (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Returned))
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaymentMethod = "COD";
                order.PaymentTransactionId = $"COD_{DateTime.Now:yyyyMMddHHmmss}";
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Trạng thái đơn hàng không hợp lệ." });
        }

        // ============================
        // Create (Tạo đơn)
        // ============================
        [Authorize(Roles = "Customer")]
        public IActionResult Create()
        {
            var vm = new OrderViewModel
            {
                Order = new Order(),
                pickupAreas = _context.Areas.ToList(),
                deliveryAreas = _context.Areas.ToList(),
                PriceTables = _context.PriceTables.Select(p => new PriceTableViewModel { AreaId = p.AreaId, BasePrice = p.BasePrice, PricePerKm = p.PricePerKm, WeightPrice = p.WeightPrice }).ToList()
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
                vm.PriceTables = _context.PriceTables.Select(p => new PriceTableViewModel { AreaId = p.AreaId, BasePrice = p.BasePrice, PricePerKm = p.PricePerKm, WeightPrice = p.WeightPrice }).ToList();
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);
            if (customer == null) { ModelState.AddModelError("", "Lỗi xác thực khách hàng."); return View(vm); }

            var order = vm.Order;
            order.CustomerId = customer.Id;
            order.CreatedAt = DateTime.Now;
            order.Status = OrderStatus.Pending;
            order.ReceiverName = vm.Order.ReceiverName;
            order.ReceiverPhone = vm.Order.ReceiverPhone;

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

            order.Payer = vm.Payer;
            order.PaymentMethod = vm.PaymentMethod;
            if (order.Payer == PayerType.Sender && order.PaymentMethod == "Online") order.PaymentStatus = PaymentStatus.ProcessingOnline;
            else order.PaymentStatus = PaymentStatus.Unpaid;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            order.TrackingCode = $"MVD{order.CreatedAt:ddMMyyyy}{order.Id:D4}";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = order.Id });
        }

        // ============================
        // CUSTOMER: My Orders
        // ============================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);
            var orders = await _context.Orders.Where(o => o.CustomerId == customer!.Id)
                .Include(o => o.PickupArea).Include(o => o.DeliveryArea)
                .Include(o => o.Dispatcher).ThenInclude(d => d!.User)
                .Include(o => o.Shipper).ThenInclude(s => s!.User)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return View(orders);
        }

        // ============================
        // Edit (Sửa đơn)
        // ============================
        [Authorize(Roles = "Admin,Dispatcher,Customer")]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (User.IsInRole("Customer"))
            {
                var user = await _userManager.GetUserAsync(User);
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);
                if (order.CustomerId != customer!.Id || order.Status != OrderStatus.Pending)
                {
                    TempData["Error"] = "Bạn chỉ được sửa đơn hàng chờ xử lý của chính mình.";
                    return RedirectToAction("Details", new { id });
                }
            }

            var vm = new OrderViewModel
            {
                Order = order,
                pickupAreas = _context.Areas.ToList(),
                deliveryAreas = _context.Areas.ToList(),
                PriceTables = _context.PriceTables.Select(p => new PriceTableViewModel { AreaId = p.AreaId, BasePrice = p.BasePrice, PricePerKm = p.PricePerKm, WeightPrice = p.WeightPrice }).ToList(),
                Payer = order.Payer,
                PaymentMethod = order.PaymentMethod
            };
            return View(vm);
        }

        [HttpPost, Authorize(Roles = "Admin,Dispatcher,Customer"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrderViewModel vm)
        {
            if (id != vm.Order.Id) return NotFound();
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.PickupAreaId = vm.Order.PickupAreaId;
            order.DeliveryAreaId = vm.Order.DeliveryAreaId;
            order.PickupAddress = vm.Order.PickupAddress;
            order.DeliveryAddress = vm.Order.DeliveryAddress;
            order.DistanceKm = vm.Order.DistanceKm;
            order.WeightKg = vm.Order.WeightKg;
            order.ReceiverName = vm.Order.ReceiverName;
            order.ReceiverPhone = vm.Order.ReceiverPhone;

            if (order.Status == OrderStatus.Pending)
            {
                order.Payer = vm.Order.Payer;
                order.PaymentMethod = vm.Order.PaymentMethod;
            }

            _context.Update(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = order.Id });
        }

        // ============================
        // Delete (Admin)
        // ============================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == id);
            return order == null ? NotFound() : View(order);
        }

        [HttpPost, ActionName("Delete"), Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null) { _context.Orders.Remove(order); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        // ============================
        // Tracking
        // ============================
        [AllowAnonymous]
        public IActionResult Tracking() => View();

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Tracking(string trackingCode)
        {
            if (string.IsNullOrWhiteSpace(trackingCode)) { ViewBag.Error = "Vui lòng nhập mã vận đơn."; return View(); }
            var order = await _context.Orders.Include(o => o.Customer).Include(o => o.Dispatcher).Include(o => o.Shipper).Include(o => o.PickupArea).Include(o => o.DeliveryArea).Include(o => o.OrderLogs).FirstOrDefaultAsync(o => o.TrackingCode == trackingCode);
            if (order == null) { ViewBag.Error = "Không tìm thấy đơn hàng."; return View(); }
            return View("Details", order);
        }

        // ============================
        // Pay (VNPay) & Callback
        // ============================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Pay(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null || order.PaymentStatus == PaymentStatus.Paid) return NotFound();

            if (order.Payer != PayerType.Sender || order.PaymentMethod != "Online" || order.PaymentStatus != PaymentStatus.ProcessingOnline)
            {
                TempData["Error"] = "Đơn hàng không đủ điều kiện thanh toán Online.";
                return RedirectToAction("Details", new { id });
            }

            var vnPay = new VnPayLibrary();
            var vnpayConfig = _configuration.GetSection("VnPay");

            vnPay.AddRequestData("vnp_Version", vnpayConfig["Version"]!);
            vnPay.AddRequestData("vnp_Command", vnpayConfig["Command"]!);
            vnPay.AddRequestData("vnp_TmnCode", vnpayConfig["TmnCode"]!);
            vnPay.AddRequestData("vnp_Amount", ((long)order.TotalPrice * 100).ToString());
            vnPay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnPay.AddRequestData("vnp_CurrCode", vnpayConfig["CurrCode"]!);
            vnPay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnPay.AddRequestData("vnp_Locale", vnpayConfig["Locale"]!);
            vnPay.AddRequestData("vnp_OrderInfo", "Thanh toan don: " + order.TrackingCode);
            vnPay.AddRequestData("vnp_OrderType", "other");
            vnPay.AddRequestData("vnp_ReturnUrl", vnpayConfig["ReturnUrl"]!);
            vnPay.AddRequestData("vnp_TxnRef", order.Id.ToString());

            return Redirect(vnPay.CreateRequestUrl(vnpayConfig["BaseUrl"]!, vnpayConfig["HashSecret"]!));
        }

        [AllowAnonymous]
        public async Task<IActionResult> PaymentCallback()
        {
            var vnpayConfig = _configuration.GetSection("VnPay");
            var vnpay = new VnPayLibrary();
            foreach (var (key, value) in Request.Query)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_")) vnpay.AddResponseData(key, value.ToString());
            }

            if (!int.TryParse(vnpay.GetResponseData("vnp_TxnRef"), out int orderId)) return RedirectToAction("Index");

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            string vnp_SecureHash = Request.Query["vnp_SecureHash"]!;
            if (vnpay.ValidateSignature(vnp_SecureHash, vnpayConfig["HashSecret"]!))
            {
                if (vnpay.GetResponseData("vnp_ResponseCode") == "00")
                {
                    if (order.PaymentStatus != PaymentStatus.Paid)
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.PaymentMethod = "Online";
                        order.PaymentTransactionId = vnpay.GetResponseData("vnp_TransactionNo");
                        _context.Update(order);
                        await _context.SaveChangesAsync();
                        TempData["Message"] = "Thanh toán thành công.";
                    }
                }
                else TempData["Error"] = "Thanh toán thất bại: " + vnpay.GetResponseData("vnp_ResponseCode");
            }
            else TempData["Error"] = "Sai chữ ký bảo mật.";

            return RedirectToAction("Details", new { id = orderId });
        }
    }
}