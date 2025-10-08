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
    public class CustomersFunction
    {
        private readonly TableClient _tableClient;
        private readonly ILogger _logger;

        public CustomersFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            var conn = config["AzureWebJobsStorage"];
            _tableClient = new TableClient(conn, "Customers");
            _logger = loggerFactory.CreateLogger<CustomersFunction>();
        }

        [Function("ListCustomers")]
        public async Task<HttpResponseData> ListCustomers(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "customers")] HttpRequestData req)
        {
            _logger.LogInformation("Fetching all customers...");

            var customers = new List<Customer>();
            await foreach (var entity in _tableClient.QueryAsync<Customer>(x => x.PartitionKey == "Customer"))
            {
                customers.Add(entity);
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(customers);
            return response;
        }

        [Function("GetCustomer")]
        public async Task<HttpResponseData> GetCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "customers/{id}")] HttpRequestData req, string id)
        {
            try
            {
                var result = await _tableClient.GetEntityAsync<Customer>("Customer", id);
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Value);
                return response;
            }
            catch
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Customer not found.");
                return notFound;
            }
        }

        [Function("CreateCustomer")]
        public async Task<HttpResponseData> CreateCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customers")] HttpRequestData req)
        {
            var newCustomer = await req.ReadFromJsonAsync<Customer>();
            if (newCustomer == null || string.IsNullOrWhiteSpace(newCustomer.Name))
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid input.");
                return bad;
            }

            newCustomer.PartitionKey = "Customer";
            newCustomer.RowKey = Guid.NewGuid().ToString();

            await _tableClient.AddEntityAsync(newCustomer);
            var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await response.WriteAsJsonAsync(newCustomer);
            return response;
        }

        [Function("UpdateCustomer")]
        public async Task<HttpResponseData> UpdateCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "customers/{id}")] HttpRequestData req, string id)
        {
            var input = await req.ReadFromJsonAsync<Customer>();
            if (input == null)
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            try
            {
                var existing = await _tableClient.GetEntityAsync<Customer>("Customer", id);
                var customer = existing.Value;

                customer.Name = input.Name ?? customer.Name;
                customer.Email = input.Email ?? customer.Email;
                customer.ShippingAddress = input.ShippingAddress ?? customer.ShippingAddress;

                await _tableClient.UpdateEntityAsync(customer, customer.ETag, TableUpdateMode.Replace);
                var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(customer);
                return ok;
            }
            catch
            {
                var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Customer not found.");
                return notFound;
            }
        }

        [Function("DeleteCustomer")]
        public async Task<HttpResponseData> DeleteCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "customers/{id}")] HttpRequestData req, string id)
        {
            await _tableClient.DeleteEntityAsync("Customer", id);
            var response = req.CreateResponse(System.Net.HttpStatusCode.NoContent);
            return response;
        }
    }
}
