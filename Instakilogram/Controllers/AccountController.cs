using Instakilogram.Models;
using Instakilogram.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinScout.RequestResponse;

namespace Instakilogram.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private IUserService Service;

        public AccountController(IUserService service)
        {
            this.Service = service;
        }

        [HttpPost]
        [Route("SignUp")]
        public async Task<IActionResult> SignUp([FromForm] string user_object, [FromForm] IFormFile? Picture)
        {
            SignUpRequest zahtev = JsonConvert.DeserializeObject<SignUpRequest>(user_object);

            if (this.Service.UserExists(zahtev.UserName, zahtev.Mail))
            {
                return BadRequest(new { message = "Korisnik vec postoji. Probajte drugi mail ili korisnicko ime, ili izvrsiti validaciju putem mejla." });
            }

            string hash, salt;
            this.Service.PasswordHash(out hash, out salt, zahtev.Password);

            User newUser = new User
            {
                UserName = zahtev.UserName,
                Name = zahtev.Name,
                Mail = zahtev.Mail,
                Password = hash,
                Salt = salt,
                Description = zahtev.Description,
                ProfilePicture = "",
                Online = false,
                PIN = null
            };

            if (Picture==null)
            {
                string picture = this.Service.AddImage(Picture);
                newUser.ProfilePicture += picture;
                this.Service.TmpStoreAccount(newUser);
            }
            else
            {
                this.Service.TmpStoreAccount(newUser, Picture);
            }

            return Ok(new { message = "Poslat vam je mail za validaciju."});
        }
        [HttpPost]
        [Route("Verify/{key}")]
        public async Task<IActionResult> ValidateAccount(string key)
        {
            string link = this.Service.ApproveAccount(key);
            if (!String.IsNullOrEmpty(link))
            {
                return Redirect(link); //stranica obavestenje/login
            }
            return BadRequest(new { message = "Doslo je do greske."});
        }

    }
}
