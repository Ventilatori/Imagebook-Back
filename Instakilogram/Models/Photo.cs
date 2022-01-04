using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.Models
{
    public class Photo
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public DateTime TimePosted { get; set; }
        public string? Description { get; set; }
    }
}
