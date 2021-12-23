using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WinScout.RequestResponse
{
    public class LogInZahtev
    {
        [Required]
        public string Mail { get; set; }

        [Required]
        public string Password { get; set; }

    }
}
