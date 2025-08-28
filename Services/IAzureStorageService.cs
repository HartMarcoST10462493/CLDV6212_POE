using System.Collections.Generic;
using System.Threading.Tasks;
using ABCRetailers.Models;
using Microsoft.AspNetCore.Http;

namespace ABCRetailers.Services
{
    public interface IAzureStorageService
    {
        // Initialization
        Task EnsureInitializedAsync();

        // Customers
        Task CreateCustomerAsync(Customer customer);
        Task<IEnumerable<Customer>> GetCustomersAsync();
        Task<Customer> GetCustomerByIdAsync(string rowKey);
        Task UpdateCustomerAsync(Customer customer);
        Task DeleteCustomerAsync(string partitionKey, string rowKey);

        // Products
        Task CreateProductAsync(Product product);
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product> GetProductByIdAsync(string rowKey);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(string partitionKey, string rowKey);

        // NEW: safe overloads that include partitionKey (names unchanged)
        Task<Product> GetProductByIdAsync(string partitionKey, string rowKey);

        // Orders
        Task CreateOrderAsync(Order order);
        Task<IEnumerable<Order>> GetOrdersAsync();
        Task<Order> GetOrderByIdAsync(string rowKey);
        Task UpdateOrderAsync(Order order);
        Task DeleteOrderAsync(string partitionKey, string rowKey);

        // NEW: safe overloads that include partitionKey (names unchanged)
        Task<Order> GetOrderByIdAsync(string partitionKey, string rowKey);

        // Blobs
        Task<string> UploadProductImageAsync(IFormFile file);

        // Queue
        Task SendOrderMessageAsync(string message);

        // Files
        Task UploadContractAsync(IFormFile file);
    }
}
