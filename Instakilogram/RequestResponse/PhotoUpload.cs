using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.RequestResponse
{
    public class PhotoUpload
    {
        [Required]
        public IFormFile Picture { get; set; }
        public string Title  { get; set; }
        public string? Description { get; set; }
        public List<string>? TaggedUsers { get; set; }
        public List<string>? Hashtags { get; set; }        
    }
}
