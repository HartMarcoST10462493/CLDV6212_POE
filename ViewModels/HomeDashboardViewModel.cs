using System.Collections.Generic;
using ABCRetailers.Models;

namespace ABCRetailers.ViewModels
{
    public class HomeDashboardViewModel
    {
        public int CustomerCount { get; set; }
        public int ProductCount { get; set; }
        public int OrderCount { get; set; }
        public List<Product> FeaturedProducts { get; set; } = new();
    }
}
