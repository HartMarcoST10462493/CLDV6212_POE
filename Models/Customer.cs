using Azure;
using Azure.Data.Tables;
using System;

namespace ABCRetailers.Models
{
    public class Customer : ITableEntity
    {
        // ITableEntity required members
        public string PartitionKey { get; set; } = "Customer";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        // Application properties
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string ShippingAddress { get; set; }
    }
}


