using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ABCRetailers.Models;

namespace ABCRetailers.Functions
{
    public class PaymentProofFunction
    {
        private readonly TableClient _orderTable;
        private readonly ILogger _logger;

        public PaymentProofFunction(IConfiguration config, ILoggerFactory loggerFactory)
        {
            var conn = config["AzureWebJobsStorage"];
            _orderTable = new TableClient(conn, "Orders");
            _logger = loggerFactory.CreateLogger<PaymentProofFunction>();
        }

        [Function("UploadPaymentProof")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Received payment proof upload request.");

            // Verify Content-Type header
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing Content-Type header.");
                return bad;
            }

            var contentType = contentTypeValues.First();
            if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid Content-Type. Use multipart/form-data.");
                return bad;
            }

            // Extract boundary and initialize multipart reader
            var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value;
            var reader = new MultipartReader(boundary, req.Body);
            MultipartSection? section;
            string? fileName = null;
            MemoryStream? fileStream = null;

            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                var hasFileContentDisposition =
                    ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition) &&
                    contentDisposition.DispositionType.Equals("form-data") &&
                    !string.IsNullOrEmpty(contentDisposition.FileName.Value);

                if (hasFileContentDisposition)
                {
                    fileName = contentDisposition.FileName.Value;
                    fileStream = new MemoryStream();
                    await section.Body.CopyToAsync(fileStream);
                    fileStream.Position = 0;
                }
            }

            if (fileStream == null || fileName == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No file uploaded. Please upload a PDF file.");
                return badResponse;
            }

            var orderId = fileName.Split('_')[0];

            try
            {
                // Upload to Blob Storage
                var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var containerClient = blobServiceClient.GetBlobContainerClient("contracts");
                await containerClient.CreateIfNotExistsAsync();

                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.UploadAsync(fileStream, overwrite: true);

                // Update Table Storage record
                var response = await _orderTable.GetEntityAsync<Order>("Order", orderId);
                var order = response.Value;
                order.Status = "Paid";
                await _orderTable.UpsertEntityAsync(order);

                _logger.LogInformation($"Order {orderId} marked as Paid.");

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync($"Payment proof for order {orderId} uploaded and marked as Paid.");
                return ok;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError($"Failed to update order {orderId}: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error: {ex.Message}");
                return err;
            }
            finally
            {
                fileStream?.Dispose();
            }
        }
    }
}
