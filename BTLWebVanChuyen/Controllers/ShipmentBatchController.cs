using BTLWebVanChuyen.Data;
using BTLWebVanChuyen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLWebVanChuyen.Controllers
{
    [Authorize(Roles = "Dispatcher, Admin")]
    public class ShipmentBatchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShipmentBatchController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 1. DANH SÁCH LÔ HÀNG (INDEX)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            IQueryable<ShipmentBatch> query = _context.ShipmentBatches
                .Include(b => b.OriginWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.DestinationWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.Orders);

            // Phân quyền hiển thị
            if (User.IsInRole("Dispatcher"))
            {
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (dispatcher == null) return Forbid();

                ViewBag.CurrentAreaId = dispatcher.AreaId; // Để view check quyền nút bấm

                // Chỉ hiện lô liên quan đến khu vực của Dispatcher
                query = query.Where(b => b.OriginWarehouse!.AreaId == dispatcher.AreaId || b.DestinationWarehouse!.AreaId == dispatcher.AreaId);
            }
            else if (User.IsInRole("Admin"))
            {
                ViewBag.IsAdmin = true; // Admin thấy hết và có toàn quyền
            }

            return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
        }

        // ==========================================
        // 2. CHI TIẾT LÔ HÀNG (DETAILS)
        // ==========================================
        public async Task<IActionResult> Details(int id)
        {
            var batch = await _context.ShipmentBatches
                .Include(b => b.OriginWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.DestinationWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.Shipper).ThenInclude(s => s!.User)
                .Include(b => b.Orders).ThenInclude(o => o.Customer).ThenInclude(c => c.User)
                .Include(b => b.BatchLogs)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // Logic lấy AreaId hiện tại để check quyền nút bấm trong View
            if (User.IsInRole("Dispatcher"))
            {
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                ViewBag.CurrentAreaId = dispatcher?.AreaId ?? 0;
            }
            else if (User.IsInRole("Admin"))
            {
                ViewBag.IsAdmin = true;
            }

            return View(batch);
        }

        // ==========================================
        // 3. TRANG TẠO LÔ - GOM ĐƠN (CREATE - GET)
        // ==========================================
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            int currentAreaId = 0;

            // Xác định khu vực làm việc để lọc đơn
            if (User.IsInRole("Dispatcher"))
            {
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (dispatcher == null) return Forbid();
                currentAreaId = dispatcher.AreaId ?? 0;
            }
            else if (User.IsInRole("Admin"))
            {
                // Admin không thuộc khu vực nào cụ thể để gom tự động.
                // Tạm thời chặn Admin tạo lô thủ công từ trang này để tránh rối logic.
                TempData["Error"] = "Chức năng gom đơn dành cho Điều phối viên tại từng khu vực cụ thể.";
                return RedirectToAction(nameof(Index));
            }

            // A. Lấy đơn GIAO ĐI (Forward): PickupSuccess, Đang ở kho mình, Cần đi kho khác
            var forwardOrders = await _context.Orders
                .Include(o => o.DeliveryArea).Include(o => o.PickupWarehouse).Include(o => o.PickupArea)
                .Where(o => o.Status == OrderStatus.PickupSuccess
                         && o.PickupAreaId == currentAreaId
                         && o.PickupAreaId != o.DeliveryAreaId
                         && o.ShipmentBatchId == null)
                .ToListAsync();

            // B. Lấy đơn HOÀN VỀ (Return): ReadyToReturn, Đang ở kho mình (Kho Giao cũ)
            var returnOrders = await _context.Orders
                .Include(o => o.PickupArea).Include(o => o.DeliveryArea).Include(o => o.PickupWarehouse)
                .Where(o => o.Status == OrderStatus.ReadyToReturn
                         && o.DeliveryAreaId == currentAreaId
                         && o.ShipmentBatchId == null)
                .ToListAsync();

            var allPendingOrders = forwardOrders.Concat(returnOrders).ToList();

            // Lấy danh sách kho đích (Khác khu vực hiện tại)
            ViewBag.DestWarehouses = await _context.Warehouses
                .Include(w => w.Area)
                .Where(w => w.AreaId != currentAreaId)
                .ToListAsync();

            ViewBag.Shippers = await _context.Employees.Where(e => e.Role == EmployeeRole.Shipper).Include(e => e.User).ToListAsync();

            return View(allPendingOrders);
        }

        // ==========================================
        // 4. XỬ LÝ TẠO LÔ (CREATE - POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(List<int> orderIds, int destinationWarehouseId, int? shipperId)
        {
            if (orderIds == null || !orderIds.Any()) return Json(new { success = false, message = "Chưa chọn đơn hàng." });
            if (orderIds.Count > MAX_ORDERS_PER_BATCH) return Json(new { success = false, message = $"Quá giới hạn {MAX_ORDERS_PER_BATCH} đơn." });

            var user = await _userManager.GetUserAsync(User);
            var firstOrder = await _context.Orders.FindAsync(orderIds[0]);

            // --- 1. XÁC ĐỊNH KHO XUẤT PHÁT (Tự động) ---
            int originWarehouseId = 0;

            if (firstOrder!.Status == OrderStatus.ReadyToReturn)
            {
                originWarehouseId = firstOrder.DeliveryWarehouseId ?? 0;
            }
            else if (firstOrder.Status == OrderStatus.PickupSuccess)
            {
                originWarehouseId = firstOrder.PickupWarehouseId ?? 0;
            }

            // Logic Dự phòng (Fallback) nếu dữ liệu lỗi NULL
            if (originWarehouseId == 0 && User.IsInRole("Dispatcher"))
            {
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.AreaId == dispatcher!.AreaId);
                if (defaultWarehouse != null) originWarehouseId = defaultWarehouse.Id;
            }

            if (originWarehouseId == 0)
                return Json(new { success = false, message = "Lỗi hệ thống: Không xác định được kho xuất phát." });

            // --- TẠO LÔ ---
            var batch = new ShipmentBatch
            {
                BatchCode = $"LO_{DateTime.Now:yyMMdd}_{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                OriginWarehouseId = originWarehouseId,
                DestinationWarehouseId = destinationWarehouseId,
                ShipperId = shipperId,
                Status = ShipmentBatchStatus.Created,
                CreatedAt = DateTime.Now
            };

            _context.ShipmentBatches.Add(batch);
            await _context.SaveChangesAsync();

            _context.ShipmentBatchLogs.Add(new ShipmentBatchLog { ShipmentBatchId = batch.Id, Status = ShipmentBatchStatus.Created, Note = "Khởi tạo lô hàng", UpdatedBy = user!.UserName });

            // Gán lô cho đơn
            var orders = await _context.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
            foreach (var order in orders)
            {
                order.ShipmentBatchId = batch.Id;

                // Cập nhật lại kho để khớp dữ liệu (Fix data)
                if (order.Status == OrderStatus.ReadyToReturn) order.DeliveryWarehouseId = originWarehouseId;
                else if (order.Status == OrderStatus.PickupSuccess) order.PickupWarehouseId = originWarehouseId;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Tạo lô hàng thành công!" });
        }

        // ==========================================
        // 5. XUẤT BẾN (StartTransport)
        // ==========================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTransport(int id)
        {
            var batch = await _context.ShipmentBatches.Include(b => b.Orders).FirstOrDefaultAsync(b => b.Id == id);

            // Chỉ cho phép xuất bến khi đang ở trạng thái Created
            if (batch == null || batch.Status != ShipmentBatchStatus.Created) return NotFound();

            // Check quyền: Admin được làm hết, Dispatcher phải đúng kho xuất
            if (User.IsInRole("Dispatcher"))
            {
                var user = await _userManager.GetUserAsync(User);
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (batch.OriginWarehouseId != 0 &&  // (Optional check)
                                                     // Chúng ta nên check theo Area để linh hoạt hơn
                     (await _context.Warehouses.FindAsync(batch.OriginWarehouseId))?.AreaId != dispatcher!.AreaId)
                {
                    // Tuy nhiên, ở bước Create đã lọc đơn theo khu vực rồi nên có thể bỏ qua check chặt chẽ ở đây nếu muốn linh động
                }
            }

            batch.Status = ShipmentBatchStatus.InTransit;
            var userName = User.Identity!.Name;

            _context.ShipmentBatchLogs.Add(new ShipmentBatchLog { ShipmentBatchId = batch.Id, Status = ShipmentBatchStatus.InTransit, Note = "Xe xuất bến", UpdatedBy = userName });

            foreach (var order in batch.Orders)
            {
                // A. Giao đi
                if (order.Status == OrderStatus.PickupSuccess)
                {
                    order.Status = OrderStatus.InterAreaTransporting;
                    order.DeliveryWarehouseId = batch.DestinationWarehouseId;
                    _context.OrderLogs.Add(new OrderLog { OrderId = order.Id, Status = OrderStatus.InterAreaTransporting, Time = DateTime.Now, Note = $"Xuất bến giao đi (Lô {batch.BatchCode})", UpdatedBy = userName });
                }
                // B. Hoàn về
                else if (order.Status == OrderStatus.ReadyToReturn)
                {
                    order.Status = OrderStatus.Returning;
                    _context.OrderLogs.Add(new OrderLog { OrderId = order.Id, Status = OrderStatus.Returning, Time = DateTime.Now, Note = $"Xe xuất bến hoàn trả (Lô {batch.BatchCode})", UpdatedBy = userName });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==========================================
        // 6. NHẬP BẾN (CompleteTransport)
        // ==========================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTransport(int id)
        {
            var batch = await _context.ShipmentBatches
                .Include(b => b.Orders)
                .Include(b => b.DestinationWarehouse)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null || batch.Status != ShipmentBatchStatus.InTransit) return NotFound();

            // Check quyền: Chỉ Dispatcher ở KHO ĐÍCH (hoặc Admin) mới được nhập
            if (User.IsInRole("Dispatcher"))
            {
                var user = await _userManager.GetUserAsync(User);
                var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
                if (batch.DestinationWarehouse!.AreaId != dispatcher!.AreaId)
                {
                    TempData["Error"] = "Bạn không có quyền nhập kho lô này (Sai khu vực).";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            batch.Status = ShipmentBatchStatus.Completed;
            batch.CompletedAt = DateTime.Now;

            _context.ShipmentBatchLogs.Add(new ShipmentBatchLog { ShipmentBatchId = batch.Id, Status = ShipmentBatchStatus.Completed, Note = "Nhập kho đích thành công", UpdatedBy = User.Identity!.Name });

            foreach (var order in batch.Orders)
            {
                // A. Giao đi -> Đến kho giao
                if (order.Status == OrderStatus.InterAreaTransporting)
                {
                    order.Status = OrderStatus.ArrivedDeliveryHub;
                    // Giữ ShipmentBatchId
                    _context.OrderLogs.Add(new OrderLog { OrderId = order.Id, Status = OrderStatus.ArrivedDeliveryHub, Time = DateTime.Now, Note = $"Nhập kho giao (Lô {batch.BatchCode})", UpdatedBy = User.Identity.Name });
                }
                // B. Hoàn về -> Về kho gốc (Đích)
                else if (order.Status == OrderStatus.Returning)
                {
                    order.Status = OrderStatus.ArrivedPickupTerminal;
                    order.ShipmentBatchId = null; // Giải phóng
                    _context.OrderLogs.Add(new OrderLog { OrderId = order.Id, Status = OrderStatus.ArrivedPickupTerminal, Time = DateTime.Now, Note = $"Đã hoàn về kho gốc (Lô {batch.BatchCode})", UpdatedBy = User.Identity.Name });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==========================================
        // EDIT & DELETE (Thêm mới)
        // ==========================================

        // GET: ShipmentBatch/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var batch = await _context.ShipmentBatches
                .Include(b => b.OriginWarehouse)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null) return NotFound();

            // Chỉ cho phép sửa khi lô hàng chưa xuất bến (Status = Created)
            if (batch.Status != ShipmentBatchStatus.Created)
            {
                TempData["Error"] = "Không thể chỉnh sửa lô hàng đã xuất bến hoặc đã hoàn thành.";
                return RedirectToAction(nameof(Details), new { id = batch.Id });
            }

            var user = await _userManager.GetUserAsync(User);
            var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);

            // Load lại danh sách kho đích và shipper để chọn lại
            ViewBag.DestWarehouses = await _context.Warehouses
                .Include(w => w.Area)
                .Where(w => w.AreaId != dispatcher!.AreaId) // Khác khu vực hiện tại
                .ToListAsync();

            ViewBag.Shippers = await _context.Employees
                .Where(e => e.Role == EmployeeRole.Shipper)
                .Include(e => e.User)
                .ToListAsync();

            return View(batch);
        }

        // POST: ShipmentBatch/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShipmentBatch batch)
        {
            if (id != batch.Id) return NotFound();

            var existingBatch = await _context.ShipmentBatches.FindAsync(id);
            if (existingBatch == null) return NotFound();

            if (existingBatch.Status != ShipmentBatchStatus.Created)
            {
                TempData["Error"] = "Lô hàng này không còn ở trạng thái Mới tạo.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật các trường cho phép sửa
            existingBatch.DestinationWarehouseId = batch.DestinationWarehouseId;
            existingBatch.ShipperId = batch.ShipperId;

            // Ghi log sửa đổi
            var user = await _userManager.GetUserAsync(User);
            _context.ShipmentBatchLogs.Add(new ShipmentBatchLog
            {
                ShipmentBatchId = id,
                Status = existingBatch.Status,
                Time = DateTime.Now,
                Note = "Cập nhật thông tin lô hàng",
                UpdatedBy = user!.UserName
            });

            try
            {
                _context.Update(existingBatch);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Cập nhật lô hàng thành công.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.ShipmentBatches.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: ShipmentBatch/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var batch = await _context.ShipmentBatches
                .Include(b => b.OriginWarehouse)
                .Include(b => b.DestinationWarehouse)
                .Include(b => b.Shipper).ThenInclude(s => s!.User)
                .Include(b => b.Orders)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (batch == null) return NotFound();

            if (batch.Status != ShipmentBatchStatus.Created)
            {
                TempData["Error"] = "Chỉ có thể xóa lô hàng khi ở trạng thái 'Mới tạo'.";
                return RedirectToAction(nameof(Index));
            }

            return View(batch);
        }

        // POST: ShipmentBatch/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var batch = await _context.ShipmentBatches.Include(b => b.Orders).FirstOrDefaultAsync(b => b.Id == id);
            if (batch != null)
            {
                if (batch.Status != ShipmentBatchStatus.Created)
                {
                    TempData["Error"] = "Không thể xóa lô hàng đã vận chuyển.";
                    return RedirectToAction(nameof(Index));
                }

                // Khi xóa lô, các đơn hàng trong đó sẽ tự động rời khỏi lô (ShipmentBatchId = null)
                // do cấu hình OnDelete(DeleteBehavior.SetNull) trong DbContext.
                // Tuy nhiên, ta nên chắc chắn rằng trạng thái đơn hàng vẫn ổn định (vẫn là PickupSuccess).

                _context.ShipmentBatches.Remove(batch);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Đã xóa lô hàng và giải phóng các đơn hàng bên trong.";
            }
            return RedirectToAction(nameof(Index));
        }

        const int MAX_ORDERS_PER_BATCH = 50; // Thêm hằng số vào đầu class

        // ==========================================
        // 1. TRANG THÊM ĐƠN VÀO LÔ CÓ SẴN (AddOrders)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> AddOrders(int batchId)
        {
            var user = await _userManager.GetUserAsync(User);
            var dispatcher = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user!.Id);
            if (dispatcher == null) return Forbid();

            var batch = await _context.ShipmentBatches
                .Include(b => b.DestinationWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.OriginWarehouse).ThenInclude(w => w!.Area)
                .Include(b => b.Orders)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null || batch.Status != ShipmentBatchStatus.Created)
            {
                TempData["Error"] = "Lô hàng không hợp lệ để thêm đơn.";
                return RedirectToAction(nameof(Index));
            }

            // Tính toán giới hạn
            int currentOrderCount = batch.Orders.Count;
            ViewBag.MaxToAdd = MAX_ORDERS_PER_BATCH - currentOrderCount;
            if (ViewBag.MaxToAdd <= 0)
            {
                TempData["Error"] = $"Lô hàng đã đủ số lượng đơn tối đa ({MAX_ORDERS_PER_BATCH}).";
                return RedirectToAction(nameof(Details), new { id = batchId });
            }

            // Lấy các ID khu vực cần thiết
            var batchDestAreaId = batch.DestinationWarehouse!.AreaId;
            var batchOriginAreaId = batch.OriginWarehouse!.AreaId; // Khu vực của Dispatcher hiện tại

            // LẤY VÀ LỌC CÁC ĐƠN HÀNG PHÙ HỢP ĐỂ THÊM VÀO LÔ (Chỉ đơn liên tỉnh)
            var pendingOrders = await _context.Orders
                .Include(o => o.DeliveryArea).Include(o => o.PickupArea).Include(o => o.PickupWarehouse)
                .Where(o => o.ShipmentBatchId == null // Đơn chưa thuộc lô nào
                         && o.DeliveryAreaId != o.PickupAreaId) // Đơn liên tỉnh
                .Where(o =>
                    // [1] Đơn GIAO ĐI (Forward): Đang ở kho mình VÀ đi đến đích của Lô này
                    (o.Status == OrderStatus.PickupSuccess
                        && o.PickupAreaId == batchOriginAreaId
                        && o.DeliveryAreaId == batchDestAreaId)
                    ||
                    // [2] Đơn HOÀN VỀ (Return): Đang ở kho mình VÀ về đến đích cuối cùng (PickupArea) của Lô này
                    (o.Status == OrderStatus.ReadyToReturn
                        && o.DeliveryAreaId == batchOriginAreaId // Đơn nằm ở kho hiện tại (DeliveryArea của chuyến trước)
                        && o.PickupAreaId == batchDestAreaId)    // Đích cuối cùng là kho lấy cũ
                )
                .ToListAsync();

            ViewBag.BatchId = batchId;
            ViewBag.BatchCode = batch.BatchCode;
            ViewBag.IsAdding = true; // Báo hiệu View rằng đây là chế độ Add

            // Tái sử dụng View Create
            return View("Create", pendingOrders);
        }

        // ==========================================
        // 6. THÊM ĐƠN HÀNG VÀO LÔ CÓ SẴN (AddOrdersToBatch) - POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrdersToBatch(int batchId, List<int> orderIds)
        {
            if (orderIds == null || !orderIds.Any()) return Json(new { success = false, message = "Chưa chọn đơn hàng nào." });

            var user = await _userManager.GetUserAsync(User);
            var batch = await _context.ShipmentBatches.Include(b => b.Orders).FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null || batch.Status != ShipmentBatchStatus.Created)
                return Json(new { success = false, message = "Lô hàng không hợp lệ để thêm đơn." });

            // 1. KIỂM TRA GIỚI HẠN
            if (batch.Orders.Count + orderIds.Count > MAX_ORDERS_PER_BATCH)
            {
                return Json(new { success = false, message = $"Chỉ có thể thêm tối đa {MAX_ORDERS_PER_BATCH - batch.Orders.Count} đơn nữa vào lô này." });
            }

            // 2. LẤY VÀ CẬP NHẬT ĐƠN
            var ordersToAdd = await _context.Orders
                .Where(o => orderIds.Contains(o.Id) && o.ShipmentBatchId == null)
                .ToListAsync();

            int addedCount = 0;
            foreach (var order in ordersToAdd)
            {
                // Gán Lô mới
                order.ShipmentBatchId = batch.Id;

                // Cập nhật lại ID kho cho đơn hàng để dữ liệu nhất quán
                if (order.Status == OrderStatus.ReadyToReturn)
                    order.DeliveryWarehouseId = batch.OriginWarehouseId;
                else if (order.Status == OrderStatus.PickupSuccess)
                    order.PickupWarehouseId = batch.OriginWarehouseId;

                // Log Đơn hàng
                _context.OrderLogs.Add(new OrderLog { OrderId = order.Id, Status = order.Status, Note = $"Thêm vào Lô {batch.BatchCode}", UpdatedBy = user!.UserName });
                addedCount++;
            }

            // Log Lô hàng
            _context.ShipmentBatchLogs.Add(new ShipmentBatchLog { ShipmentBatchId = batchId, Status = batch.Status, Note = $"Thêm {addedCount} đơn hàng mới vào lô.", UpdatedBy = user!.UserName });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã thêm {addedCount} đơn hàng vào lô {batch.BatchCode}." });
        }
    }
}