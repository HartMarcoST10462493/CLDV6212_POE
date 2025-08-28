using Azure;
using Azure.Data.Tables;
using System;

namespace ABCRetailers.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Product";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        public string ProductName { get; set; }
        public string Description { get; set; }
        // Table storage doesn't support decimal directly; use double for price.
        public double Price { get; set; }
        public int StockAvailable { get; set; }
        public string? ImageUrl { get; set; }  // Made optional
    }
}
