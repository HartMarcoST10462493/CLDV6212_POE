using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;

namespace ABCRetailers.Functions
{
    public class ProductsFunction
    {
        private readonly TableClient _tableClient;
        private readonly ILogger _logger;

        public ProductsFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            var conn = config["AzureWebJobsStorage"];
            _tableClient = new TableClient(conn, "Products");
            _logger = loggerFactory.CreateLogger<ProductsFunction>();
        }

        [Function("ListProducts")]
        public async Task<HttpResponseData> ListProducts(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "products")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching all products...");

            var products = new List<Product>();
            await foreach (var entity in _tableClient.QueryAsync<Product>(x => x.PartitionKey == "Product"))
            {
                products.Add(entity);
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(products);
            return response;
        }

        [Function("GetProduct")]
        public async Task<HttpResponseData> GetProduct(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "products/{id}")] HttpRequestData req, string id)
        {
            try
            {
                var result = await _tableClient.GetEntityAsync<Product>("Product", id);
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Value);
                return response;
            }
            catch
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Product not found.");
                return notFound;
            }
        }

        [Function("CreateProduct")]
        public async Task<HttpResponseData> CreateProduct(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products")] HttpRequestData req)
        {
            var newProduct = await req.ReadFromJsonAsync<Product>();
            if (newProduct == null || string.IsNullOrWhiteSpace(newProduct.ProductName))
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid input.");
                return bad;
            }

            newProduct.PartitionKey = "Product";
            newProduct.RowKey = Guid.NewGuid().ToString();

            await _tableClient.AddEntityAsync(newProduct);
            var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await response.WriteAsJsonAsync(newProduct);
            return response;
        }

        [Function("UpdateProduct")]
        public async Task<HttpResponseData> UpdateProduct(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "products/{id}")] HttpRequestData req, string id)
        {
            var input = await req.ReadFromJsonAsync<Product>();
            if (input == null)
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            try
            {
                var existing = await _tableClient.GetEntityAsync<Product>("Product", id);
                var product = existing.Value;

                product.ProductName = input.ProductName ?? product.ProductName;
                product.Description = input.Description ?? product.Description;
                product.Price = input.Price > 0 ? input.Price : product.Price;
                product.StockAvailable = input.StockAvailable > 0 ? input.StockAvailable : product.StockAvailable;
                product.ImageUrl = input.ImageUrl ?? product.ImageUrl;

                await _tableClient.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);
                var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(product);
                return ok;
            }
            catch
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Product not found.");
                return notFound;
            }
        }

        [Function("DeleteProduct")]
        public async Task<HttpResponseData> DeleteProduct(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "products/{id}")] HttpRequestData req, string id)
        {
            await _tableClient.DeleteEntityAsync("Product", id);
            var response = req.CreateResponse(System.Net.HttpStatusCode.NoContent);
            return response;
        }
    }
}
