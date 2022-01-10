using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.RequestResponse
{
    public class PhotoUpload
    {
        public string? Description { get; set; }
        public List<string>? TaggedUsers { get; set; }
        public List<string>? Hashtags { get; set; }
        public IFormFile? Picture { get; set; }
    }
}
