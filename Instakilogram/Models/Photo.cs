using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.Models
{
    public class Photo
    {
        public string Path { get; set; }
        public DateTime TimePosted { get; set; }
        public string? Description { get; set; }

        public int NumberOfLikes { get; set; }
    }
}
