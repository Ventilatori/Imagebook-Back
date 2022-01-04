using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.Models
{
    public class Photoupload
    {
        public long ID { get; set; }
        public string imagepath { get; set; }
        public DateTime InsertedOn { get; set; }
    }
}
