using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace ABCRetailers.Controllers
{
    [Route("Payment/[action]")] // Keeps URL as /Payment/Upload
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storage;

        public UploadController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        // GET: /Payment/Upload
        [HttpGet("Upload")]
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Payment/Upload
        [HttpPost("Upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (model.ProofOfPayment != null)
            {
                var extension = Path.GetExtension(model.ProofOfPayment.FileName).ToLower();

                if (extension == ".pdf")
                {
                    // Proof of Payment PDFs → File Share
                    await _storage.UploadContractAsync(model.ProofOfPayment);
                    ViewBag.Message = "PDF uploaded successfully (stored in File Share)";
                }
                else
                {
                    // Other files (images etc.) → Blob
                    var blobUrl = await _storage.UploadProductImageAsync(model.ProofOfPayment);
                    ViewBag.Message = "Image uploaded successfully (stored in Blob Storage)";
                    ViewBag.BlobUrl = blobUrl; // optional: show blob URL
                }
            }

            return View(model);
        }
    }
}
