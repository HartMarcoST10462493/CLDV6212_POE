using ABCRetailers.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ABCRetailers.Services
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly string _connectionString;
        private readonly string _customerTableName;
        private readonly string _productTableName;
        private readonly string _orderTableName;
        private readonly string _blobContainerName;
        private readonly string _queueName;
        private readonly string _shareName;

        private readonly TableClient _customerTable;
        private readonly TableClient _productTable;
        private readonly TableClient _orderTable;
        private readonly BlobContainerClient _blobContainer;
        private readonly QueueClient _queueClient;
        private readonly ShareClient _shareClient;

        public AzureStorageService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("AzureStorage");
            _customerTableName = config["AzureStorage:CustomerTable"] ?? "Customers";
            _productTableName = config["AzureStorage:ProductTable"] ?? "Products";
            _orderTableName = config["AzureStorage:OrderTable"] ?? "Orders";
            _blobContainerName = config["AzureStorage:ProductBlobContainer"] ?? "product-images";
            _queueName = config["AzureStorage:OrderQueueName"] ?? "orderqueue";
            _shareName = config["AzureStorage:ContractsShareName"] ?? "contracts";

            var tableService = new TableServiceClient(_connectionString);
            _customerTable = tableService.GetTableClient(_customerTableName);
            _productTable = tableService.GetTableClient(_productTableName);
            _orderTable = tableService.GetTableClient(_orderTableName);

            _blobContainer = new BlobContainerClient(_connectionString, _blobContainerName);
            _queueClient = new QueueClient(_connectionString, _queueName);
            _shareClient = new ShareClient(_connectionString, _shareName);
        }

        public async Task EnsureInitializedAsync()
        {
            await _customerTable.CreateIfNotExistsAsync();
            await _productTable.CreateIfNotExistsAsync();
            await _orderTable.CreateIfNotExistsAsync();

            await _blobContainer.CreateIfNotExistsAsync();
            await _queueClient.CreateIfNotExistsAsync();
            await _shareClient.CreateIfNotExistsAsync();
        }

        // ===========================
        // Customers
        // ===========================
        public async Task CreateCustomerAsync(Customer customer)
        {
            if (string.IsNullOrEmpty(customer.RowKey)) customer.RowKey = Guid.NewGuid().ToString();
            customer.PartitionKey ??= "Customer";
            await _customerTable.AddEntityAsync(customer);
        }

        public async Task<IEnumerable<Customer>> GetCustomersAsync()
        {
            var results = new List<Customer>();
            var query = _customerTable.QueryAsync<Customer>();
            await foreach (var item in query) results.Add(item);
            return results;
        }

        public async Task<Customer> GetCustomerByIdAsync(string rowKey)
        {
            try
            {
                var resp = await _customerTable.GetEntityAsync<Customer>("Customer", rowKey);
                return resp.Value;
            }
            catch (RequestFailedException) { return null; }
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            customer.PartitionKey ??= "Customer";
            await _customerTable.UpsertEntityAsync(customer);
        }

        public async Task DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            try { await _customerTable.DeleteEntityAsync(partitionKey, rowKey); } catch { }
        }

        // ===========================
        // Products
        // ===========================
        public async Task CreateProductAsync(Product product)
        {
            if (string.IsNullOrEmpty(product.RowKey)) product.RowKey = Guid.NewGuid().ToString();
            product.PartitionKey ??= "Product";
            await _productTable.AddEntityAsync(product);
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            var list = new List<Product>();
            var query = _productTable.QueryAsync<Product>();
            await foreach (var item in query) list.Add(item);
            return list;
        }

        public async Task<Product> GetProductByIdAsync(string rowKey)
        {
            try
            {
                var resp = await _productTable.GetEntityAsync<Product>("Product", rowKey);
                return resp.Value;
            }
            catch (RequestFailedException) { return null; }
        }

        public async Task<Product> GetProductByIdAsync(string partitionKey, string rowKey)
        {
            try
            {
                var resp = await _productTable.GetEntityAsync<Product>(partitionKey, rowKey);
                return resp.Value;
            }
            catch (RequestFailedException) { return null; }
        }

        public async Task UpdateProductAsync(Product product)
        {
            product.PartitionKey ??= "Product";
            await _productTable.UpsertEntityAsync(product);
        }

        public async Task DeleteProductAsync(string partitionKey, string rowKey)
        {
            try { await _productTable.DeleteEntityAsync(partitionKey, rowKey); } catch { }
        }

        // ===========================
        // Orders
        // ===========================
        public async Task CreateOrderAsync(Order order)
        {
            if (string.IsNullOrEmpty(order.RowKey)) order.RowKey = Guid.NewGuid().ToString();
            order.PartitionKey ??= "Order";
            await _orderTable.AddEntityAsync(order);
        }

        public async Task<IEnumerable<Order>> GetOrdersAsync()
        {
            var list = new List<Order>();
            var query = _orderTable.QueryAsync<Order>();
            await foreach (var item in query) list.Add(item);
            return list.OrderByDescending(o => o.OrderDate);
        }

        public async Task<Order> GetOrderByIdAsync(string rowKey)
        {
            try
            {
                var resp = await _orderTable.GetEntityAsync<Order>("Order", rowKey);
                return resp.Value;
            }
            catch (RequestFailedException) { return null; }
        }

        public async Task<Order> GetOrderByIdAsync(string partitionKey, string rowKey)
        {
            try
            {
                var resp = await _orderTable.GetEntityAsync<Order>(partitionKey, rowKey);
                return resp.Value;
            }
            catch (RequestFailedException) { return null; }
        }

        public async Task UpdateOrderAsync(Order order)
        {
            order.PartitionKey ??= "Order";
            await _orderTable.UpsertEntityAsync(order);
        }

        public async Task DeleteOrderAsync(string partitionKey, string rowKey)
        {
            try { await _orderTable.DeleteEntityAsync(partitionKey, rowKey); } catch { }
        }

        // ===========================
        // Blobs
        // ===========================
        public async Task<string> UploadProductImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var safeName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var blobClient = _blobContainer.GetBlobClient(safeName);
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, overwrite: true);

            return blobClient.Uri.ToString();
        }

        // ===========================
        // Queue
        // ===========================
        public async Task SendOrderMessageAsync(string message)
        {
            await _queueClient.CreateIfNotExistsAsync();
            await _queueClient.SendMessageAsync(message);
        }

        // ===========================
        // File Share (Proof of Payment)
        // ===========================
        public async Task UploadContractAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return;

            // Ensure share exists
            await _shareClient.CreateIfNotExistsAsync();

            // Root directory
            var root = _shareClient.GetRootDirectoryClient();

            string fileName = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + file.FileName;
            var fileClient = root.GetFileClient(fileName);

            using var stream = file.OpenReadStream();
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new Azure.HttpRange(0, stream.Length), stream);
        }

        // ===========================
        // Date helpers
        // ===========================
        public static string ToShortDateString(DateTimeOffset dto) => dto.LocalDateTime.ToShortDateString();
        public static string ToShortDateString(DateTimeOffset? dto) => dto.HasValue ? dto.Value.LocalDateTime.ToShortDateString() : "N/A";
    }
}
