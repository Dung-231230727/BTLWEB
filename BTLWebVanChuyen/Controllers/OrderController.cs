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
        // HÀM TIỆN ÍCH: Tạo thông báo
        // ============================
        private Task CreateNotificationAsync(string userId, string message, int? orderId = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;
            return CreateNotificationsAsync(new[] { userId }, message, orderId);
        }

        private async Task CreateNotificationsAsync(IEnumerable<string> userIds, string message, int? orderId = null)
        {
            var recipients = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (!recipients.Any() || string.IsNullOrWhiteSpace(message))
                return;

            foreach (var uid in recipients)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    OrderId = orderId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string?> GetCustomerUserIdAsync(int customerId)
        {
            return await _context.Customers
                .Where(c => c.Id == customerId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
        }

        private async Task<string?> GetEmployeeUserIdAsync(int? employeeId)
        {
            if (!employeeId.HasValue) return null;

            return await _context.Employees
                .Where(e => e.Id == employeeId.Value)
                .Select(e => e.UserId)
                .FirstOrDefaultAsync();
        }

        private async Task<List<string>> GetDispatchersByAreaAsync(int areaId)
        {
            return await _context.Employees
                .Where(e => e.Role == EmployeeRole.Dispatcher && e.AreaId == areaId)
                .Select(e => e.UserId)
                .ToListAsync();
        }

        private async Task NotifyOrderStakeholdersAsync(Order order, string message, string? excludeUserId = null)
        {
            var recipients = new List<string>();

            var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
            if (!string.IsNullOrWhiteSpace(customerUserId)) recipients.Add(customerUserId);

            var dispatcherUserId = await GetEmployeeUserIdAsync(order.DispatcherId);
            if (!string.IsNullOrWhiteSpace(dispatcherUserId)) recipients.Add(dispatcherUserId);

            var shipperUserId = await GetEmployeeUserIdAsync(order.ShipperId);
            if (!string.IsNullOrWhiteSpace(shipperUserId)) recipients.Add(shipperUserId);

            if (!string.IsNullOrWhiteSpace(excludeUserId))
            {
                recipients = recipients.Where(id => id != excludeUserId).ToList();
            }

            if (!recipients.Any()) return;

            await CreateNotificationsAsync(recipients, message, order.Id);
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

                // === LỌC THEO GIAI ĐOẠN VẬN CHUYỂN ===
                // Giai đoạn thuộc Khu vực Lấy
                var pickupPhaseStatuses = new List<OrderStatus> {
                    OrderStatus.Pending, OrderStatus.AssignedPickupShipper, OrderStatus.Picking,
                    OrderStatus.PickupSuccess, OrderStatus.PickupFailed, OrderStatus.Cancelled,
                    OrderStatus.Returning, OrderStatus.ArrivedPickupTerminal,
                    OrderStatus.AssignedReturnShipper, OrderStatus.ReturningToSender,
                    OrderStatus.Returned, OrderStatus.ReturnFailed
                };

                // Giai đoạn thuộc Khu vực Giao
                var deliveryPhaseStatuses = new List<OrderStatus> {
                    OrderStatus.InterAreaTransporting,
                    OrderStatus.ArrivedDeliveryHub,
                    OrderStatus.AssignedDeliveryShipper,
                    OrderStatus.Delivering,
                    OrderStatus.Delivered,
                    OrderStatus.DeliveryFailed
                };

                query = query.Where(o =>
                    (o.PickupAreaId == dispatcher.AreaId && pickupPhaseStatuses.Contains(o.Status))
                    ||
                    (o.DeliveryAreaId == dispatcher.AreaId && deliveryPhaseStatuses.Contains(o.Status))
                );
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
                .Include(o => o.PickupWarehouse)
                .Include(o => o.DeliveryWarehouse)
                .Include(o => o.OrderLogs)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Nếu là Dispatcher, kiểm tra quyền xem chi tiết và chuẩn bị dữ liệu
            if (User.IsInRole("Dispatcher"))
            {
                var user = await _userManager.GetUserAsync(User);
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);

                if (dispatcher != null)
                {
                    // === KIỂM TRA BẢO MẬT ===
                    var pickupPhaseStatuses = new[] {
                        OrderStatus.Pending, OrderStatus.AssignedPickupShipper, OrderStatus.Picking,
                        OrderStatus.PickupSuccess, OrderStatus.PickupFailed, OrderStatus.Cancelled,
                        OrderStatus.Returning, OrderStatus.ArrivedPickupTerminal,
                        OrderStatus.AssignedReturnShipper, OrderStatus.ReturningToSender,
                        OrderStatus.Returned, OrderStatus.ReturnFailed
                    };
                    var deliveryPhaseStatuses = new[] {
                        OrderStatus.InterAreaTransporting, OrderStatus.ArrivedDeliveryHub,
                        OrderStatus.AssignedDeliveryShipper, OrderStatus.Delivering,
                        OrderStatus.Delivered, OrderStatus.DeliveryFailed
                    };

                    bool isAuthorized = false;
                    if (order.PickupAreaId == dispatcher.AreaId && pickupPhaseStatuses.Contains(order.Status)) isAuthorized = true;
                    if (order.DeliveryAreaId == dispatcher.AreaId && deliveryPhaseStatuses.Contains(order.Status)) isAuthorized = true;

                    if (!isAuthorized)
                    {
                        return View("Error", new ErrorViewModel { RequestId = "AccessDenied - Bạn không có quyền xem đơn hàng này tại thời điểm hiện tại." });
                    }

                    // --- Chuẩn bị dữ liệu View ---
                    ViewBag.CurrentDispatcherAreaId = dispatcher.AreaId;

                    ViewBag.Shippers = await _context.Employees
                        .Where(e => e.Role == EmployeeRole.Shipper && e.AreaId == dispatcher.AreaId)
                        .Include(e => e.User)
                        .ToListAsync();

                    ViewBag.Warehouses = await _context.Warehouses
                        .Where(w => w.AreaId == dispatcher.AreaId)
                        .ToListAsync();

                    if (order.PickupAreaId == dispatcher.AreaId && order.PickupAreaId != order.DeliveryAreaId)
                    {
                        ViewBag.DeliveryWarehouses = await _context.Warehouses
                            .Where(w => w.AreaId == order.DeliveryAreaId)
                            .ToListAsync();
                    }
                }
            }
            return View(order);
        }

        // ============================================================
        // 1. LOGIC ĐIỀU PHỐI (ASSIGN)
        // ============================================================
        [HttpPost, Authorize(Roles = "Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int orderId, int shipperId, int? warehouseId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var shipper = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == shipperId);
            var user = await _userManager.GetUserAsync(User);
            var dispatcher = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.UserId == user!.Id);

            if (order == null || shipper == null || dispatcher == null)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            if (order.Payer == PayerType.Sender && order.PaymentMethod == "Online" && order.PaymentStatus != PaymentStatus.Paid)
                return Json(new { success = false, message = "Đơn hàng chưa được thanh toán Online." });

            bool isSameArea = order.PickupAreaId == order.DeliveryAreaId;

            // --- GIAI ĐOẠN 1: GÁN LẤY HÀNG ---
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PickupFailed)
            {
                if (dispatcher.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Bạn không có quyền điều phối lấy hàng (Khác khu vực)." });
                if (shipper.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Shipper phải thuộc khu vực lấy hàng." });
                if (warehouseId == null) return Json(new { success = false, message = "Vui lòng chọn Kho nhập." });

                order.Status = OrderStatus.AssignedPickupShipper;
                order.PickupWarehouseId = warehouseId;
            }
            // --- GIAI ĐOẠN 2: GÁN GIAO HÀNG ---
            else if (order.Status == OrderStatus.ArrivedDeliveryHub ||
                    (order.Status == OrderStatus.PickupSuccess && isSameArea) ||
                     order.Status == OrderStatus.DeliveryFailed)
            {
                if (dispatcher.AreaId != order.DeliveryAreaId)
                    return Json(new { success = false, message = "Bạn không có quyền điều phối giao hàng (Khác khu vực)." });
                if (shipper.AreaId != order.DeliveryAreaId)
                    return Json(new { success = false, message = "Shipper phải thuộc khu vực giao hàng." });

                order.Status = OrderStatus.AssignedDeliveryShipper;
                order.DeliveryWarehouseId = warehouseId;
            }
            // --- GIAI ĐOẠN 3: GÁN HOÀN TRẢ ---
            else if (order.Status == OrderStatus.ArrivedPickupTerminal)
            {
                if (dispatcher.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Bạn không có quyền điều phối hoàn trả (Khác khu vực)." });
                if (shipper.AreaId != order.PickupAreaId)
                    return Json(new { success = false, message = "Shipper phải thuộc khu vực lấy hàng." });

                order.Status = OrderStatus.AssignedReturnShipper;
            }
            else
            {
                return Json(new { success = false, message = "Trạng thái đơn hàng không cho phép gán Shipper lúc này." });
            }

            order.ShipperId = shipper.Id;
            order.DispatcherId = dispatcher.Id;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = order.Status,
                Time = DateTime.Now,
                Note = $"Điều phối bởi {dispatcher.User!.FullName}",
                UpdatedBy = dispatcher.User.UserName
            });

            await _context.SaveChangesAsync();

            // Gửi thông báo
            var shipperMessage = "";
            if (order.Status == OrderStatus.AssignedPickupShipper)
                shipperMessage = $"Bạn được giao LẤY đơn {order.TrackingCode}.";
            else if (order.Status == OrderStatus.AssignedDeliveryShipper)
                shipperMessage = $"Bạn được giao GIAO đơn {order.TrackingCode}.";
            else if (order.Status == OrderStatus.AssignedReturnShipper)
                shipperMessage = $"Bạn được giao HOÀN TRẢ đơn {order.TrackingCode} cho người gửi.";

            await CreateNotificationAsync(shipper.UserId, shipperMessage, order.Id);

            var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
            if (!string.IsNullOrWhiteSpace(customerUserId))
            {
                var msg = order.Status == OrderStatus.AssignedReturnShipper
                    ? $"Đơn {order.TrackingCode} đang được hoàn trả lại cho bạn."
                    : $"Đơn {order.TrackingCode} đã được gán cho shipper {shipper.User!.FullName}.";
                await CreateNotificationAsync(customerUserId, msg, order.Id);
            }

            var whName = await _context.Warehouses.Where(w => w.Id == warehouseId).Select(w => w.Name).FirstOrDefaultAsync();
            return Json(new
            {
                success = true,
                statusDisplay = order.Status.GetDisplayName(),
                dispatcherName = dispatcher.User?.FullName,
                shipperName = shipper.User?.FullName,
                warehouseName = whName
            });
        }

        // ============================================================
        // 2. UPDATE STATUS
        // ============================================================
        [HttpPost, Authorize(Roles = "Shipper,Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus status)
        {
            var order = await _context.Orders
                .Include(o => o.PickupArea).Include(o => o.DeliveryArea)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            bool isSameArea = order.PickupAreaId == order.DeliveryAreaId;
            var user = await _userManager.GetUserAsync(User);
            bool isValidTransition = false;

            // --- A. LOGIC CỦA SHIPPER ---
            if (User.IsInRole("Shipper"))
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (order.ShipperId != currentEmployee?.Id)
                    return Json(new { success = false, message = "Bạn không phải Shipper phụ trách." });

                // 1. LẤY HÀNG
                if (order.Status == OrderStatus.AssignedPickupShipper && status == OrderStatus.Picking) isValidTransition = true;
                else if (order.Status == OrderStatus.Picking)
                {
                    if (status == OrderStatus.PickupFailed)
                    {
                        if (order.PaymentStatus == PaymentStatus.Paid)
                            return Json(new { success = false, message = "Bạn đã xác nhận thu tiền, không thể báo Lấy thất bại." });
                        isValidTransition = true;
                    }
                    else if (status == OrderStatus.PickupSuccess)
                    {
                        if (order.Payer == PayerType.Sender && order.PaymentMethod == "COD" && order.PaymentStatus != PaymentStatus.Paid)
                            return Json(new { success = false, message = "Bạn phải xác nhận 'Đã thu tiền' từ người gửi trước khi cập nhật Lấy thành công." });
                        isValidTransition = true;
                    }
                }
                // 2. GIAO HÀNG
                else if (order.Status == OrderStatus.AssignedDeliveryShipper && status == OrderStatus.Delivering) isValidTransition = true;
                else if (order.Status == OrderStatus.Delivering)
                {
                    if (status == OrderStatus.DeliveryFailed)
                    {
                        if (order.PaymentStatus == PaymentStatus.Paid && order.Payer == PayerType.Receiver)
                            return Json(new { success = false, message = "Bạn đã xác nhận thu tiền từ người nhận, không thể báo Giao thất bại." });
                        isValidTransition = true;
                    }
                    else if (status == OrderStatus.Delivered)
                    {
                        if (order.Payer == PayerType.Receiver && order.PaymentStatus != PaymentStatus.Paid)
                            return Json(new { success = false, message = "Bạn phải xác nhận 'Đã thu tiền' từ người nhận trước khi cập nhật Giao thành công." });
                        isValidTransition = true;
                    }
                }
                // 3. HOÀN TRẢ
                else if (order.Status == OrderStatus.Returning && status == OrderStatus.ArrivedPickupTerminal) isValidTransition = true;
                else if (order.Status == OrderStatus.AssignedReturnShipper && status == OrderStatus.ReturningToSender) isValidTransition = true;
                else if (order.Status == OrderStatus.ReturningToSender)
                {
                    if (status == OrderStatus.Returned)
                    {
                        // === [LOGIC MỚI] HOÀN TIỀN VÍ ===
                        // Chỉ hoàn tiền nếu: Đã thanh toán (Paid) VÀ Người gửi là người trả (Sender)
                        if (order.PaymentStatus == PaymentStatus.Paid && order.Payer == PayerType.Sender)
                        {
                            var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
                            if (!string.IsNullOrWhiteSpace(customerUserId))
                            {
                                // Cộng tiền vào ví khách
                                await ProcessWalletTransaction(
                                    customerUserId,
                                    order.TotalPrice, // Số dương (+)
                                    "REFUND",
                                    $"Hoàn tiền đơn hàng {order.TrackingCode} (Trả hàng thành công)",
                                    order.Id
                                );

                                // Ghi chú vào Log
                                if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
                                order.OrderLogs.Add(new OrderLog
                                {
                                    OrderId = order.Id,
                                    Status = status,
                                    Time = DateTime.Now,
                                    Note = "Hệ thống tự động hoàn tiền vào ví người gửi.",
                                    UpdatedBy = "System"
                                });
                            }
                        }
                        // ================================
                        isValidTransition = true;
                    }
                    else if (status == OrderStatus.ReturnFailed)
                    {
                        isValidTransition = true;
                    }
                }
            }
            // --- B. LOGIC CỦA DISPATCHER ---
            else if (User.IsInRole("Dispatcher"))
            {
                if (order.Status == OrderStatus.PickupSuccess && !isSameArea && status == OrderStatus.InterAreaTransporting) isValidTransition = true;
                else if (order.Status == OrderStatus.InterAreaTransporting && status == OrderStatus.ArrivedDeliveryHub)
                {
                    order.ShipperId = null;
                    isValidTransition = true;
                }
                else if (status == OrderStatus.Cancelled || status == OrderStatus.Returning) isValidTransition = true;
                else if (order.Status == OrderStatus.Returning && status == OrderStatus.ArrivedPickupTerminal) isValidTransition = true;
            }

            if (!isValidTransition) return Json(new { success = false, message = "Chuyển đổi trạng thái không hợp lệ hoặc thiếu điều kiện." });

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

            var statusDisplay = order.Status.GetDisplayName();
            await NotifyOrderStakeholdersAsync(order, $"Đơn {order.TrackingCode} vừa chuyển sang trạng thái {statusDisplay}.", user?.Id);

            return Json(new { success = true, statusDisplay });
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

                var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
                if (!string.IsNullOrWhiteSpace(customerUserId))
                {
                    await CreateNotificationAsync(customerUserId, $"Đơn {order.TrackingCode} đã được xác nhận thu COD thành công.", order.Id);
                }
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

            // Khởi tạo log đầu tiên
            order.OrderLogs = new List<OrderLog>
            {
                new OrderLog
                {
                    Status = OrderStatus.Pending,
                    Time = DateTime.Now,
                    Note = "Khách hàng tạo đơn hàng mới.",
                    UpdatedBy = user!.UserName
                }
            };

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

            await CreateNotificationAsync(customer.UserId, $"Đơn hàng {order.TrackingCode} đã được tạo thành công. Chúng tôi sẽ xử lý ngay.", order.Id);

            var dispatcherUserIds = await GetDispatchersByAreaAsync(order.PickupAreaId);
            if (dispatcherUserIds.Any())
            {
                await CreateNotificationsAsync(dispatcherUserIds, $"Có đơn hàng mới {order.TrackingCode} cần điều phối tại khu vực của bạn.", order.Id);
            }

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

            var user = await _userManager.GetUserAsync(User);
            var editorName = user?.FullName ?? user?.UserName ?? "Hệ thống";
            await NotifyOrderStakeholdersAsync(order, $"Đơn {order.TrackingCode} đã được chỉnh sửa bởi {editorName}.", user?.Id);

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
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Dispatcher).ThenInclude(d => d!.User)
                .Include(o => o.Shipper).ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                var trackingCode = order.TrackingCode;
                var recipients = new List<string>();

                var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
                if (!string.IsNullOrWhiteSpace(customerUserId)) recipients.Add(customerUserId);

                var dispatcherUserId = await GetEmployeeUserIdAsync(order.DispatcherId);
                if (!string.IsNullOrWhiteSpace(dispatcherUserId)) recipients.Add(dispatcherUserId);

                var shipperUserId = await GetEmployeeUserIdAsync(order.ShipperId);
                if (!string.IsNullOrWhiteSpace(shipperUserId)) recipients.Add(shipperUserId);

                if (recipients.Any())
                {
                    await CreateNotificationsAsync(recipients, $"Đơn hàng {trackingCode} đã bị xóa bởi quản trị viên.", null);
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }
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
            if (string.IsNullOrWhiteSpace(trackingCode))
            {
                ViewBag.Error = "Vui lòng nhập mã vận đơn.";
                return View();
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.TrackingCode == trackingCode);

            if (order == null)
            {
                ViewBag.Error = "Không tìm thấy đơn hàng.";
                return View();
            }

            // Chuyển sang action public để tránh bị chặn bởi [Authorize] trên Details
            return RedirectToAction("PublicDetails", new { id = order.Id });
        }

        [AllowAnonymous]
        public async Task<IActionResult> PublicDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer).ThenInclude(c => c.User)
                .Include(o => o.Dispatcher).ThenInclude(d => d!.User)
                .Include(o => o.Shipper).ThenInclude(s => s!.User)
                .Include(o => o.PickupArea)
                .Include(o => o.DeliveryArea)
                .Include(o => o.PickupWarehouse)
                .Include(o => o.DeliveryWarehouse)
                .Include(o => o.OrderLogs)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Nếu cần ẩn thông tin nhạy cảm cho người public, build một ViewModel và chỉ truyền fields cần thiết.
            return View("Details", order);
        }

        // ============================
        // Pay (VNPay)
        // ============================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Pay(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null || order.PaymentStatus == PaymentStatus.Paid) return NotFound();

            // Sửa: Cho phép thanh toán lại nếu trạng thái là Unpaid hoặc ProcessingOnline
            if (order.Payer != PayerType.Sender || order.PaymentMethod != "Online")
            {
                TempData["Error"] = "Đơn hàng không đủ điều kiện thanh toán Online.";
                return RedirectToAction("Details", new { id });
            }

            var vnPay = new VnPayLibrary();
            var vnpayConfig = _configuration.GetSection("VnPay");

            vnPay.AddRequestData("vnp_Version", "2.1.0");
            vnPay.AddRequestData("vnp_Command", "pay");
            vnPay.AddRequestData("vnp_TmnCode", vnpayConfig["TmnCode"] ?? "");
            vnPay.AddRequestData("vnp_Amount", ((long)order.TotalPrice * 100).ToString());
            vnPay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnPay.AddRequestData("vnp_CurrCode", "VND");

            // Sửa: Lấy IP, nếu localhost ::1 thì lấy 127.0.0.1
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1") ipAddress = "127.0.0.1";
            vnPay.AddRequestData("vnp_IpAddr", ipAddress);

            vnPay.AddRequestData("vnp_Locale", "vn");
            vnPay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang:" + order.TrackingCode);
            vnPay.AddRequestData("vnp_OrderType", "other");
            vnPay.AddRequestData("vnp_ReturnUrl", vnpayConfig["ReturnUrl"] ?? "");

            // Sửa: Thêm Tick vào TxnRef để tránh lỗi trùng lặp khi thanh toán lại
            vnPay.AddRequestData("vnp_TxnRef", $"{order.Id}_{DateTime.Now.Ticks}");

            return Redirect(vnPay.CreateRequestUrl(vnpayConfig["BaseUrl"] ?? "", vnpayConfig["HashSecret"] ?? ""));
        }

        [AllowAnonymous]
        public async Task<IActionResult> PaymentCallback()
        {
            var vnpayConfig = _configuration.GetSection("VnPay");
            var vnpay = new VnPayLibrary();
            foreach (var (key, value) in Request.Query)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value.ToString());
                }
            }

            // Sửa: Tách OrderId từ vnp_TxnRef (dạng OrderId_Ticks)
            var vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");
            var orderIdStr = vnp_TxnRef.Split('_')[0];

            if (!int.TryParse(orderIdStr, out int orderId)) return RedirectToAction("Index");

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            string vnp_SecureHash = Request.Query["vnp_SecureHash"].ToString(); // Sửa: đảm bảo .ToString()
            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnpayConfig["HashSecret"] ?? "");

            if (checkSignature)
            {
                if (vnpay.GetResponseData("vnp_ResponseCode") == "00")
                {
                    if (order.PaymentStatus != PaymentStatus.Paid)
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.PaymentMethod = "Online";
                        // Lưu mã giao dịch đầy đủ của VNPay
                        order.PaymentTransactionId = vnpay.GetResponseData("vnp_TransactionNo");

                        _context.Update(order);
                        await _context.SaveChangesAsync();

                        // ... (Giữ nguyên phần gửi thông báo) ...
                        var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
                        if (!string.IsNullOrWhiteSpace(customerUserId))
                        {
                            await CreateNotificationAsync(customerUserId, $"Thanh toán Online cho đơn {order.TrackingCode} đã thành công.", order.Id);
                        }

                        TempData["Message"] = "Thanh toán thành công.";
                    }
                }
                else
                {
                    // Mã lỗi từ VNPay
                    TempData["Error"] = "Thanh toán thất bại. Mã lỗi: " + vnpay.GetResponseData("vnp_ResponseCode");
                }
            }
            else
            {
                TempData["Error"] = "Có lỗi xảy ra trong quá trình xử lý (Sai chữ ký bảo mật).";
            }

            return RedirectToAction("Details", new { id = orderId });
        }

        // ============================
        // ConfirmCodCollection
        // ============================
        [HttpPost, Authorize(Roles = "Shipper"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCodCollection(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var user = await _userManager.GetUserAsync(User);
            var shipper = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);

            if (order == null || shipper == null) return Json(new { success = false, message = "Lỗi dữ liệu." });

            if (order.ShipperId != shipper.Id) return Json(new { success = false, message = "Bạn không phụ trách đơn này." });

            if (order.PaymentStatus == PaymentStatus.Paid) return Json(new { success = false, message = "Đơn hàng đã được thanh toán trước đó." });

            order.PaymentStatus = PaymentStatus.Paid;
            order.PaymentTransactionId = $"COD_COLLECTED_BY_{user!.UserName}_{DateTime.Now:yyyyMMddHHmmss}";

            // === [MỚI] TRỪ TIỀN VÀO VÍ SHIPPER (CÔNG NỢ) ===
            // Shipper cầm tiền mặt -> Ví điện tử bị trừ tương ứng
            await ProcessWalletTransaction(
                shipper.UserId,
                -order.TotalPrice, // Số âm
                "COD_DEDUCT",
                $"Thu hộ COD đơn hàng {order.TrackingCode}",
                order.Id
            );

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = order.Status,
                Time = DateTime.Now,
                Note = $"Shipper {user.FullName} xác nhận đã thu tiền COD.",
                UpdatedBy = user.UserName
            });

            await _context.SaveChangesAsync();

            var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
            if (!string.IsNullOrWhiteSpace(customerUserId))
            {
                await CreateNotificationAsync(customerUserId, $"Shipper {user!.FullName} đã xác nhận thu tiền COD cho đơn {order.TrackingCode}.", order.Id);
            }

            var dispatcherUserId = await GetEmployeeUserIdAsync(order.DispatcherId);
            if (!string.IsNullOrWhiteSpace(dispatcherUserId))
            {
                await CreateNotificationAsync(dispatcherUserId, $"Shipper {user!.FullName} đã xác nhận thu tiền COD cho đơn {order.TrackingCode}.", order.Id);
            }

            return Json(new { success = true });
        }

        // ============================
        // Cancel Order
        // ============================
        [HttpPost, Authorize(Roles = "Customer"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);

            if (order == null || customer == null) return Json(new { success = false, message = "Lỗi dữ liệu." });

            if (order.CustomerId != customer.Id) return Json(new { success = false, message = "Bạn không sở hữu đơn hàng này." });

            if (order.Status != OrderStatus.Pending) return Json(new { success = false, message = "Đơn hàng đã được tiếp nhận, không thể hủy." });

            order.Status = OrderStatus.Cancelled;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = OrderStatus.Cancelled,
                Time = DateTime.Now,
                Note = $"Khách hàng {user!.FullName} đã hủy đơn.",
                UpdatedBy = user.UserName
            });

            await _context.SaveChangesAsync();

            var recipients = new List<string>();
            var dispatcherUserIds = await GetDispatchersByAreaAsync(order.PickupAreaId);
            recipients.AddRange(dispatcherUserIds);

            if (order.PickupAreaId != order.DeliveryAreaId)
            {
                var deliveryDispatcherUserIds = await GetDispatchersByAreaAsync(order.DeliveryAreaId);
                recipients.AddRange(deliveryDispatcherUserIds);
            }

            var assignedDispatcherUserId = await GetEmployeeUserIdAsync(order.DispatcherId);
            if (!string.IsNullOrWhiteSpace(assignedDispatcherUserId) && !recipients.Contains(assignedDispatcherUserId))
                recipients.Add(assignedDispatcherUserId);

            var shipperUserId = await GetEmployeeUserIdAsync(order.ShipperId);
            if (!string.IsNullOrWhiteSpace(shipperUserId)) recipients.Add(shipperUserId);

            // Thông báo cho customer (chính chủ)
            recipients.Add(customer.UserId);

            // Loại bỏ trùng lặp và gửi thông báo
            recipients = recipients.Distinct().ToList();

            var messageForOthers =
                $"Đơn {order.TrackingCode} đã bị hủy bởi khách hàng {user.FullName}.";

            var messageForCustomer =
                $"Đơn {order.TrackingCode} đã bị hủy bởi bạn.";

            var otherReceivers = recipients.Where(id => id != customer.UserId).ToList();

            // 1. Gửi cho các role khác (Dispatcher, Shipper)
            if (otherReceivers.Any())
            {
                await CreateNotificationsAsync(otherReceivers,
                    messageForOthers,
                    order.Id);
            }

            // 2. Gửi cho chính khách hàng
            await CreateNotificationAsync(customer.UserId,
                messageForCustomer,
                order.Id);

            return Json(new { success = true });
        }

        // ============================
        // StartTransfer
        // ============================
        [HttpPost, Authorize(Roles = "Dispatcher"), ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTransfer(int orderId, int deliveryWarehouseId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            var user = await _userManager.GetUserAsync(User);
            var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);

            if (order == null || dispatcher == null) return Json(new { success = false, message = "Lỗi dữ liệu." });

            if (dispatcher.AreaId != order.PickupAreaId) return Json(new { success = false, message = "Bạn không có quyền thực hiện chuyển hàng đi." });

            if (order.Status != OrderStatus.PickupSuccess) return Json(new { success = false, message = "Trạng thái đơn hàng không hợp lệ." });

            order.Status = OrderStatus.InterAreaTransporting;
            order.DeliveryWarehouseId = deliveryWarehouseId;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();
            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                Status = OrderStatus.InterAreaTransporting,
                Time = DateTime.Now,
                Note = $"Bắt đầu chuyển đến kho đích (ID: {deliveryWarehouseId})",
                UpdatedBy = user!.UserName
            });

            await _context.SaveChangesAsync();

            var whName = await _context.Warehouses.Where(w => w.Id == deliveryWarehouseId).Select(w => w.Name).FirstOrDefaultAsync();

            var customerUserId = await GetCustomerUserIdAsync(order.CustomerId);
            if (!string.IsNullOrWhiteSpace(customerUserId))
            {
                await CreateNotificationAsync(customerUserId, $"Đơn {order.TrackingCode} đã bắt đầu vận chuyển đến kho {whName} (khu vực giao).", order.Id);
            }

            var deliveryDispatcherUserIds = await GetDispatchersByAreaAsync(order.DeliveryAreaId);
            if (deliveryDispatcherUserIds.Any())
            {
                await CreateNotificationsAsync(deliveryDispatcherUserIds, $"Đơn {order.TrackingCode} đang được vận chuyển đến kho {whName} (khu vực của bạn).", order.Id);
            }

            return Json(new { success = true, message = $"Đã bắt đầu vận chuyển đến {whName}" });
        }

        // ============================
        // CUSTOMER: Tạo lại đơn hàng (Re-order)
        // ============================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Reorder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);

            if (customer == null || order.CustomerId != customer.Id)
            {
                return NotFound();
            }

            var vm = new OrderViewModel
            {
                Order = new Order
                {
                    PickupAreaId = order.PickupAreaId,
                    DeliveryAreaId = order.DeliveryAreaId,
                    PickupAddress = order.PickupAddress,
                    DeliveryAddress = order.DeliveryAddress,
                    DistanceKm = order.DistanceKm,
                    WeightKg = order.WeightKg,
                    ReceiverName = order.ReceiverName,
                    ReceiverPhone = order.ReceiverPhone,
                },
                Payer = order.Payer,
                PaymentMethod = order.PaymentMethod,
                pickupAreas = _context.Areas.ToList(),
                deliveryAreas = _context.Areas.ToList(),
                PriceTables = _context.PriceTables.Select(p => new PriceTableViewModel
                {
                    AreaId = p.AreaId,
                    BasePrice = p.BasePrice,
                    PricePerKm = p.PricePerKm,
                    WeightPrice = p.WeightPrice
                }).ToList()
            };
            return View("Create", vm);
        }

        // ============================
        // HÀM TIỆN ÍCH: Xử lý giao dịch ví
        // ============================
        private async Task ProcessWalletTransaction(string userId, decimal amount, string type, string description, int orderId)
        {
            // 1. Tìm hoặc tạo ví nếu chưa có
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0 };
                _context.Wallets.Add(wallet);
            }

            // 2. Cập nhật số dư
            wallet.Balance += amount;
            wallet.LastUpdated = DateTime.Now;

            // 3. Ghi lịch sử
            var transaction = new WalletTransaction
            {
                Wallet = wallet,
                Amount = amount,
                Type = type,
                Description = description,
                RelatedOrderId = orderId,
                CreatedAt = DateTime.Now
            };
            _context.WalletTransactions.Add(transaction);

            // Lưu ý: Không gọi SaveChanges ở đây để gộp chung transaction với hàm chính
        }
    }
}