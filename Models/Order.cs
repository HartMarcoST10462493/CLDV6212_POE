using Azure;
using Azure.Data.Tables;
using System;

namespace ABCRetailers.Models
{
    public class Order : ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        public string CustomerId { get; set; }   // maps to Customer.RowKey
        public string Username { get; set; }
        public string ProductId { get; set; }    // maps to Product.RowKey
        public string ProductName { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public int Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double TotalPrice { get; set; }
        public string Status { get; set; }

        // Constructor to initialize defaults
        public Order()
        {
            PartitionKey = "Order";
            RowKey = Guid.NewGuid().ToString();
            OrderDate = DateTimeOffset.UtcNow;
            Status = "Pending";
        }
    }
}
