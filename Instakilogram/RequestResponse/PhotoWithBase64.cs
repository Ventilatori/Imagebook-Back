﻿using Imagebook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imagebook.RequestResponse
{
    public class PhotoWithBase64
    {
      
        public Photo Metadata { get; set; }
        public string Base64Content { get; set; }

        public string? CallerEmail { get; set; }

    }
}
