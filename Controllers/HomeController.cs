using ABCRetailers.Services;
using ABCRetailers.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace ABCRetailers.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAzureStorageService _storage;

        public HomeController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _storage.GetCustomersAsync();
            var products = await _storage.GetProductsAsync();
            var orders = await _storage.GetOrdersAsync();

            var vm = new HomeDashboardViewModel
            {
                CustomerCount = customers.Count(),
                ProductCount = products.Count(),
                OrderCount = orders.Count(),
                FeaturedProducts = products.Take(4).ToList() // show top 4
            };

            return View(vm);
        }
    }
}
