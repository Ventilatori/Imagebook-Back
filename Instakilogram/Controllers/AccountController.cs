using Instakilogram.Models;
using Instakilogram.RequestResponse;
using Instakilogram.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
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
        private IGraphClient Neo;

        public AccountController(IUserService service, IGraphClient gc)
        {
            this.Service = service;
            this.Neo = gc;
        }

        [HttpPost]
        [Route("SignUp")]
        public async Task<IActionResult> SignUp([FromForm] string user_object, [FromForm] IFormFile? Picture)
        {
            SignUpRequest request = JsonConvert.DeserializeObject<SignUpRequest>(user_object);

            if (this.Service.UserExists(request.UserName, request.Mail))
            {
                return BadRequest(new { message = "Korisnik vec postoji. Probajte drugi mail ili korisnicko ime, ili izvrsiti validaciju putem mejla." });
            }

            string hash, salt;
            this.Service.PasswordHash(out hash, out salt, request.Password);

            User newUser = new User
            {
                UserName = request.UserName,
                Name = request.Name,
                Mail = request.Mail,
                Password = hash,
                Salt = salt,
                Description = request.Description,
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

        [HttpPost]
        [Route("ChangeAccountInfo")]
        public async Task<IActionResult> ChangeAccountInfo([FromForm] string? user_object, [FromForm] IFormFile? Picture)
        {
            //===cookie===
            //string mail = this.Service.ExtractUserFromCookie();
            //da li u user_object (ChangeAccountRequest) da se pakuje cookie (ili je u hederu paketa)

            //zbog toga sto nema cookie pa ne znamo koji je nalog u pitanju, zbog debug-a ubaci cu hardkodirane podatke
            string mail = "andrija.djordjevic.97@gmail.com";

            Neo4jClient.Cypher.ICypherFluentQuery query = this.Neo.Cypher
                    .Match("(n:User)")
                    .Where((User n) => n.Mail == mail);

            ChangeAccountRequest request = JsonConvert.DeserializeObject<ChangeAccountRequest>(user_object);

            //User user = this.Service.GetUserFromDb(mail); //da li user moze da se updatuje u NEO4j ako mu se prosledi ceo objekat, il za svaki propery mora SET klauzula
            if(request!=null)
            {
                //query = this.Neo.Cypher
                //    .Match("(n:User)")
                //    .Where((User n) => n.Mail == mail);

                if (!String.IsNullOrEmpty(request.UserName))
                {
                    if (this.Service.UserExists(request.UserName))
                    {
                        return BadRequest(new { message = "Korisnik vec postoji. Probajte drugi mail ili korisnicko ime, ili izvrsiti validaciju putem mejla." });
                    }
                    query.Set("n.userName = $new_user_name")
                        .WithParam("new_user_name", request.UserName);
                }
                if (!String.IsNullOrEmpty(request.Name))
                {
                    query.Set("n.name = $new_name")
                        .WithParam("new_name", request.Name);
                }
                if (!String.IsNullOrEmpty(request.Description))
                {
                    query.Set("n.description = $new_description")
                        .WithParam("new_description", request.Description);
                }
                await query.ExecuteWithoutResultsAsync();
            }
            if (Picture != null)
            {
                //query = this.Neo.Cypher
                //    .Match("(n:User)")
                //    .Where((User n) => n.Mail == mail);

                User user = query.Return(n => n.As<User>())
                    .ResultsAsync.Result.ToList().Single();
                this.Service.DeleteImage(user.ProfilePicture, IUserService.ImageType.Profile);
                string picture = this.Service.AddImage(Picture, IUserService.ImageType.Profile);
                await query.Set("n.profilePicture = $new_profile_picture")
                    .WithParam("new_profile_picture", picture)
                    .ExecuteWithoutResultsAsync();
            }

            return Ok();

        }

    }
}
