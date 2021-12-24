using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WinScout.RequestResponse
{
    public class SignUpRequest
    {
        [Required]
        public string Mail { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required]
        public string Password { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

    }
}
