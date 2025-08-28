using ABCRetailers.Models;
using ABCRetailers.Services;
using ABCRetailers.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ABCRetailers.Controllers
{
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storage;

        public OrderController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var orders = await _storage.GetOrdersAsync();
            return View(orders.OrderByDescending(o => o.OrderDate));
        }

        // GET: Create
        public async Task<IActionResult> Create()
        {
            var vm = new OrderCreateViewModel
            {
                Customers = await _storage.GetCustomersAsync(),
                Products = await _storage.GetProductsAsync()
            };
            return View(vm);
        }

        // POST: Create
        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Customers = await _storage.GetCustomersAsync();
                vm.Products = await _storage.GetProductsAsync();
                return View(vm);
            }

            var customer = await _storage.GetCustomerByIdAsync(vm.CustomerId);
            var product = await _storage.GetProductByIdAsync("Product", vm.ProductId);

            if (customer == null || product == null)
            {
                ModelState.AddModelError("", "Invalid Customer or Product");
                vm.Customers = await _storage.GetCustomersAsync();
                vm.Products = await _storage.GetProductsAsync();
                return View(vm);
            }

            if (vm.Quantity > product.StockAvailable)
            {
                ModelState.AddModelError("", "Insufficient stock");
                vm.Customers = await _storage.GetCustomersAsync();
                vm.Products = await _storage.GetProductsAsync();
                return View(vm);
            }

            var order = new Order
            {
                PartitionKey = "Order",
                CustomerId = customer.RowKey,
                Username = customer.Username,
                ProductId = product.RowKey,
                ProductName = product.ProductName,
                Quantity = vm.Quantity,
                UnitPrice = product.Price,
                TotalPrice = vm.Quantity * product.Price,
                OrderDate = DateTimeOffset.UtcNow,
                Status = "Pending"
            };

            await _storage.CreateOrderAsync(order);

            product.StockAvailable -= vm.Quantity;
            await _storage.UpdateProductAsync(product);

            await _storage.SendOrderMessageAsync($"NewOrder:{order.RowKey}");

            return RedirectToAction(nameof(Index));
        }

        // GET: Details
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            var order = await _storage.GetOrderByIdAsync(partitionKey, rowKey);
            if (order == null) return NotFound();
            return View(order);
        }

        // GET: Edit
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            var order = await _storage.GetOrderByIdAsync(partitionKey, rowKey);
            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey, Order formModel)
        {
            var existing = await _storage.GetOrderByIdAsync(partitionKey, rowKey);
            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(existing);
            }

            // Only allow safe field changes
            existing.Status = formModel.Status;

            await _storage.UpdateOrderAsync(existing);
            return RedirectToAction(nameof(Index));
        }

        // GET: Delete
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            var order = await _storage.GetOrderByIdAsync(partitionKey, rowKey);
            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            await _storage.DeleteOrderAsync(partitionKey, rowKey);
            return RedirectToAction(nameof(Index));
        }
    }
}
