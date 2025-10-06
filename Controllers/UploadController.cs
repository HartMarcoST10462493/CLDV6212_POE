using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using ABCRetailers.Models;
using ABCRetailers.Services;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace ABCRetailers.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<UploadController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public UploadController(
            IAzureStorageService storageService,
            ILogger<UploadController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _storageService = storageService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            string uploadedFileName = null;

            try
            {
                // Validate file type and size
                if (model.ProofOfPayment != null)
                {
                    // Get file extension and MIME type for better validation
                    var fileExtension = Path.GetExtension(model.ProofOfPayment.FileName)?.ToLower();
                    var contentType = model.ProofOfPayment.ContentType?.ToLower();

                    // Check file extension
                    if (fileExtension != ".pdf")
                    {
                        ModelState.AddModelError("ProofOfPayment", "Only PDF files are allowed. Please select a PDF file.");
                    }
                    // Also check MIME type as additional validation
                    else if (contentType != "application/pdf" && contentType != "application/octet-stream")
                    {
                        ModelState.AddModelError("ProofOfPayment", "Invalid file type. Please select a valid PDF file.");
                    }

                    // Check file size (10MB limit)
                    if (model.ProofOfPayment.Length > 10 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProofOfPayment", "File size exceeds 10MB limit. Please choose a smaller file.");
                    }

                    // Check if file is empty
                    if (model.ProofOfPayment.Length == 0)
                    {
                        ModelState.AddModelError("ProofOfPayment", "The selected file is empty. Please choose a valid PDF file.");
                    }
                }
                else
                {
                    ModelState.AddModelError("ProofOfPayment", "Please select a PDF file to upload.");
                }

                // Validate Order ID exists in the database
                if (!string.IsNullOrEmpty(model.OrderId))
                {
                    var order = await _storageService.GetOrderByIdAsync(model.OrderId);
                    if (order == null)
                    {
                        ModelState.AddModelError("OrderId", "Order ID not found. Please check the Order ID and try again.");
                    }
                    else
                    {
                        // Validate customer name matches the order
                        if (!string.IsNullOrEmpty(model.CustomerName) &&
                            !order.Username.Equals(model.CustomerName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError("CustomerName", "Customer name does not match the order. Please verify the customer name.");
                        }

                        // Check if order is already completed or processing
                        if (order.Status == "Completed")
                        {
                            ModelState.AddModelError("OrderId", "This order has already been completed and cannot accept new payments.");
                        }
                        else if (order.Status == "Processing")
                        {
                            ModelState.AddModelError("OrderId", "This order is already being processed. Please wait for confirmation.");
                        }
                        else if (order.Status == "Cancelled")
                        {
                            ModelState.AddModelError("OrderId", "This order has been cancelled and cannot accept payments.");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    // Get the order again to ensure we have the latest version
                    var order = await _storageService.GetOrderByIdAsync(model.OrderId);
                    if (order != null)
                    {
                        // Generate unique file name
                        uploadedFileName = $"{model.OrderId}_{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(model.ProofOfPayment.FileName)}";

                        // Update order status to "Processing"
                        order.Status = "Processing";
                        await _storageService.UpdateOrderAsync(order);

                        // Upload the proof of payment using your existing file share method
                        await _storageService.UploadContractAsync(model.ProofOfPayment);

                        // Call Azure Function to process the payment proof
                        var functionSuccess = await CallUploadPaymentProofFunction(model.OrderId, uploadedFileName, order.Username, order.TotalPrice);

                        if (functionSuccess)
                        {
                            ViewBag.Message = "Proof of payment uploaded successfully! Order status has been updated to Processing. Payment verification process has been started.";
                            ViewBag.IsSuccess = true;
                        }
                        else
                        {
                            // Still show success but warn about function call
                            ViewBag.Message = "Proof of payment uploaded successfully! Order status updated to Processing. Note: Payment verification service was unavailable but your upload was saved.";
                            ViewBag.IsSuccess = true;
                        }

                        // Clear the form
                        ModelState.Clear();
                        model = new FileUploadModel();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Order not found. Please try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading proof of payment for order {OrderId}", model.OrderId);
                ModelState.AddModelError("", $"An error occurred while processing your upload: {ex.Message}");
            }

            return View(model);
        }

        private async Task<bool> CallUploadPaymentProofFunction(string orderId, string fileName, string customerName, double totalAmount)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();

                // Use your local function URL
                var functionUrl = "http://localhost:7185/api/UploadPaymentProof";

                var payload = new
                {
                    OrderId = orderId,
                    FileName = fileName,
                    CustomerName = customerName,
                    TotalAmount = totalAmount,
                    UploadTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Status = "Processing"
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Azure Function for order {OrderId} at {FunctionUrl}", orderId, functionUrl);

                var response = await httpClient.PostAsync(functionUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully called UploadPaymentProof function for order {OrderId}. Status: {StatusCode}",
                        orderId, response.StatusCode);
                    return true;
                }
                else
                {
                    _logger.LogWarning("UploadPaymentProof function returned non-success status for order {OrderId}. Status: {StatusCode}, Response: {Response}",
                        orderId, response.StatusCode, await response.Content.ReadAsStringAsync());
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call UploadPaymentProof function for order {OrderId}", orderId);
                return false;
            }
        }

        // AJAX method to check order
        public async Task<JsonResult> CheckOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return Json(new { exists = false });
            }

            var order = await _storageService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                return Json(new { exists = false });
            }

            return Json(new
            {
                exists = true,
                customerName = order.Username,
                currentStatus = order.Status,
                totalAmount = order.TotalPrice
            });
        }
    }
}