using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ABCRetailers.Controllers
{
    public class ProductController : Controller
    {
        private readonly IAzureStorageService _storage;
        public ProductController(IAzureStorageService storage) { _storage = storage; }

        public async Task<IActionResult> Index()
        {
            var products = await _storage.GetProductsAsync();
            return View(products);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile image)
        {
            if (!ModelState.IsValid) return View(model);

            if (image != null)
            {
                var url = await _storage.UploadProductImageAsync(image);
                model.ImageUrl = url;
            }

            model.PartitionKey = "Product"; // enforce server-side
            await _storage.CreateProductAsync(model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            var product = await _storage.GetProductByIdAsync(partitionKey, rowKey);
            if (product == null) return NotFound();
            return View(product);
        }

        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            var product = await _storage.GetProductByIdAsync(partitionKey, rowKey);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey, Product formModel, IFormFile image)
        {
            // Re-load from storage to prevent PK/RK tampering
            var existing = await _storage.GetProductByIdAsync(partitionKey, rowKey);
            if (existing == null) return NotFound();

            if (!ModelState.IsValid) return View(existing);

            // Only update safe, editable fields
            existing.ProductName = formModel.ProductName;
            existing.Description = formModel.Description;
            existing.Price = formModel.Price;
            existing.StockAvailable = formModel.StockAvailable;

            if (image != null)
            {
                var url = await _storage.UploadProductImageAsync(image);
                existing.ImageUrl = url;
            }

            existing.PartitionKey = "Product"; // enforce
            await _storage.UpdateProductAsync(existing);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            var product = await _storage.GetProductByIdAsync(partitionKey, rowKey);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            await _storage.DeleteProductAsync(partitionKey, rowKey);
            return RedirectToAction(nameof(Index));
        }
    }
}
