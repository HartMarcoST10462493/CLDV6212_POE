using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;

namespace ABCRetailers.Functions
{
    public class OrderQueueFunction
    {
        private readonly TableClient _orderTable;
        private readonly ILogger _logger;

        public OrderQueueFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            try
            {
                _logger = loggerFactory.CreateLogger<OrderQueueFunction>();
                _logger.LogInformation("🔧 ORDER QUEUE FUNCTION CONSTRUCTOR STARTED");

                var conn = config.GetConnectionString("AzureStorage") ?? config["AzureStorage"];
                if (string.IsNullOrWhiteSpace(conn))
                {
                    _logger.LogCritical("❌ AzureStorage connection string is missing or empty!");
                    throw new InvalidOperationException("AzureStorage connection string not configured.");
                }

                _logger.LogInformation("🔗 Connection string successfully retrieved.");

                // Initialize via TableServiceClient for stability
                var serviceClient = new TableServiceClient(conn);
                _orderTable = serviceClient.GetTableClient("Orders");

                _logger.LogInformation("🔧 Ensuring Orders table exists...");
                _orderTable.CreateIfNotExists();

                _logger.LogInformation("✅ Table client initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical($"💥 CONSTRUCTOR FAILED: {ex.Message}");
                _logger?.LogCritical(ex.StackTrace);
                throw;
            }
        }

        [Function("ProcessOrderMessage")]
        public async Task Run([QueueTrigger("orderqueue", Connection = "AzureStorage")] string message)
        {
            _logger.LogInformation("🎯 QUEUE FUNCTION EXECUTION STARTED");

            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogError("❌ EMPTY MESSAGE - Nothing to process.");
                    return;
                }

                _logger.LogInformation($"📨 STEP 1: Received message: '{message}'");

                // Expecting format "NewOrder:<order-id>"
                if (!message.StartsWith("NewOrder:", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError($"❌ INVALID MESSAGE FORMAT: '{message}'");
                    _logger.LogError("💡 Expected format: 'NewOrder:<order-id>'");
                    return;
                }

                var orderId = message.Substring("NewOrder:".Length).Trim();
                if (string.IsNullOrWhiteSpace(orderId))
                {
                    _logger.LogError("❌ EMPTY ORDER ID - Cannot process message.");
                    return;
                }

                _logger.LogInformation($"📦 STEP 2: Parsed Order ID = '{orderId}'");
                _logger.LogInformation($"🔍 STEP 3: Fetching order entity (PartitionKey='Order', RowKey='{orderId}')...");

                try
                {
                    var response = await _orderTable.GetEntityAsync<Order>("Order", orderId);
                    var order = response.Value;

                    _logger.LogInformation($"✅ STEP 4: ORDER FOUND:");
                    _logger.LogInformation($"   RowKey: {order.RowKey}");
                    _logger.LogInformation($"   Customer: {order.Username}");
                    _logger.LogInformation($"   Status: {order.Status}");

                    _logger.LogInformation($"🔄 STEP 5: Updating status to 'Processing'...");
                    order.Status = "Processing";
                    await _orderTable.UpsertEntityAsync(order, TableUpdateMode.Replace);

                    _logger.LogInformation($"🎉 STEP 6: Order '{order.RowKey}' updated successfully.");
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning($"⚠️ ORDER NOT FOUND - No entity with RowKey='{orderId}' in Orders table.");
                    _logger.LogWarning("💡 The order exists in the queue but not in the table. Skipping message.");
                    return; // Prevent poison queue retry
                }
                catch (Exception ex)
                {
                    _logger.LogError($"💥 ERROR WHILE RETRIEVING/UPDATING ORDER: {ex}");
                    throw; // Let Azure retry for transient issues
                }

                _logger.LogInformation("🏁 QUEUE FUNCTION COMPLETED SUCCESSFULLY.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"💥 CRITICAL FAILURE IN QUEUE FUNCTION 💥");
                _logger.LogCritical($"Error: {ex.Message}");
                _logger.LogCritical($"Stack Trace: {ex.StackTrace}");
                throw; // Re-throw for Azure retry (transient or unexpected failures)
            }
        }
    }
}
