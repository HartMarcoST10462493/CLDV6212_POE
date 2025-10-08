using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;

namespace ABCRetailers.Functions
{
    public class OrderCleanupFunction
    {
        private readonly TableClient _orderTable;
        private readonly ILogger _logger;

        public OrderCleanupFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            var conn = config["AzureWebJobsStorage"];
            _orderTable = new TableClient(conn, "Orders");
            _logger = loggerFactory.CreateLogger<OrderCleanupFunction>();
        }

        [Function("CleanupStaleOrders")]
        public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
        {
            _logger.LogInformation($"Cleanup function executed at: {DateTime.UtcNow}");

            var orders = _orderTable.Query<Order>(o => o.Status == "Pending");
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

            foreach (var order in orders.Where(o => o.OrderDate < cutoff))
            {
                order.Status = "Cancelled";
                await _orderTable.UpsertEntityAsync(order);
                _logger.LogInformation($"Order {order.RowKey} cancelled (stale).");
            }
        }
    }
}
