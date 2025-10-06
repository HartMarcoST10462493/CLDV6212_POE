using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models
{
    public class FileUploadModel
    {
        [Required(ErrorMessage = "Proof of payment file is required")]
        public IFormFile ProofOfPayment { get; set; }

        [Required(ErrorMessage = "Order ID is required")]
        [Display(Name = "Order ID")]
        public string OrderId { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; }
    }
}