using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;

namespace ABCRetailers.Functions
{
    public class ProductImageFunction
    {
        private readonly TableClient _productTable;
        private readonly ILogger _logger;
        private readonly string _storageAccountName;

        public ProductImageFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            // Use your application storage connection string
            var conn = config.GetConnectionString("AzureStorage") ??
                      config["AzureStorage"] ??
                      throw new InvalidOperationException("AzureStorage connection string is not configured");

            _productTable = new TableClient(conn, "Products");
            _logger = loggerFactory.CreateLogger<ProductImageFunction>();
            _storageAccountName = GetStorageAccountNameFromConnectionString(conn);

            _logger.LogInformation("ProductImageFunction initialized with table: Products");
            _logger.LogInformation($"Storage account: {_storageAccountName}");
        }

        [Function("ProcessProductImage")]
        public async Task Run([BlobTrigger("product-images/{name}", Connection = "AzureStorage")] Stream image, string name)
        {
            _logger.LogInformation($"New product image uploaded: {name}, Size: {image.Length} bytes");

            try
            {
                // Ensure table exists
                await _productTable.CreateIfNotExistsAsync();

                // More flexible product matching - try different strategies
                var rowKey = Path.GetFileNameWithoutExtension(name);
                _logger.LogInformation($"Looking for product with RowKey: {rowKey}");

                Product product = null;

                // Strategy 1: Try exact RowKey match
                try
                {
                    var response = await _productTable.GetEntityAsync<Product>("Product", rowKey);
                    product = response.Value;
                    _logger.LogInformation($"Found product using exact RowKey match: {rowKey}");
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogInformation($"No product found with exact RowKey: {rowKey}");

                    // Strategy 2: Try to find by scanning if needed (for more complex scenarios)
                    // This is less efficient but more flexible
                    await TryFindProductByScanning(name, rowKey);
                    return;
                }

                if (product != null)
                {
                    // Update the product image URL
                    product.ImageUrl = $"https://{_storageAccountName}.blob.core.windows.net/product-images/{name}";

                    await _productTable.UpsertEntityAsync(product, TableUpdateMode.Replace);

                    _logger.LogInformation($"Product {rowKey} image URL updated to: {product.ImageUrl}");
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning($"Product not found for image {name}. No product with RowKey: {Path.GetFileNameWithoutExtension(name)}");
                // Don't throw - this isn't a system error, just no matching product
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing product image {name}: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
                throw; // This will cause retry for system errors
            }
        }

        private async Task TryFindProductByScanning(string blobName, string rowKey)
        {
            try
            {
                _logger.LogInformation($"Scanning products to find match for image: {blobName}");

                // Query all products to find a potential match
                var products = _productTable.QueryAsync<Product>();
                Product matchedProduct = null;
                string matchedField = null;

                await foreach (var product in products)
                {
                    // Check if product name matches filename (case insensitive)
                    if (product.ProductName?.Equals(rowKey, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        matchedProduct = product;
                        matchedField = "ProductName";
                        break;
                    }

                    // Check if RowKey contains part of filename or vice versa
                    if (product.RowKey?.Contains(rowKey, StringComparison.OrdinalIgnoreCase) == true ||
                        rowKey.Contains(product.RowKey, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedProduct = product;
                        matchedField = "RowKey (partial)";
                        break;
                    }
                }

                if (matchedProduct != null)
                {
                    matchedProduct.ImageUrl = $"https://{_storageAccountName}.blob.core.windows.net/product-images/{blobName}";
                    await _productTable.UpsertEntityAsync(matchedProduct, TableUpdateMode.Replace);

                    _logger.LogInformation($"Product {matchedProduct.RowKey} matched by {matchedField}. Image URL updated.");
                }
                else
                {
                    _logger.LogWarning($"No product found matching image: {blobName} after scanning all products");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during product scanning for {blobName}: {ex.Message}");
            }
        }

        private string GetStorageAccountNameFromConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "shaheemsfastfood"; // Fallback to your account name

            try
            {
                // Extract storage account name from connection string
                if (connectionString.Contains("AccountName="))
                {
                    var start = connectionString.IndexOf("AccountName=") + 12;
                    var end = connectionString.IndexOf(';', start);
                    if (end == -1) end = connectionString.Length;
                    return connectionString.Substring(start, end - start);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract storage account name: {ex.Message}");
            }

            return "shaheemsfastfood"; // Fallback
        }
    }
}