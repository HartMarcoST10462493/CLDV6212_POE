using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ABCRetailers.Models;

namespace ABCRetailers.ViewModels
{
    public class OrderCreateViewModel
    {
        [Required(ErrorMessage = "Customer is required")]
        public string CustomerId { get; set; }   // must be string (your dropdown values are string keys)

        [Required(ErrorMessage = "Product is required")]
        public string ProductId { get; set; }    // must also be string

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public IEnumerable<Customer> Customers { get; set; } = new List<Customer>();
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
    }
}
