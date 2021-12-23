using Instakilogram.Models;
using Instakilogram.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Neo4jClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class APIController : ControllerBase
    {
        private IGraphClient Neo;
        private IUserService Service;
        public APIController(IGraphClient gc, IOptions<IUserService> us)
        {
            this.Neo = gc;
            this.Service = us.Value;
        }


        //[HttpGet]
        //[Route("LogIn")]
        //public async Task<IActionResult> SignIn([FromBody] LogInZahtev zahtev)
        //{
        //    return Ok();
        //}

        [HttpGet]
        [Route("preuzmi")]
        public async Task<IActionResult> Preuzmi()
        {
            var rez = await this.Neo.Cypher
                .Match("(n:User)")
                .Return(n => n.Head().CollectAs<User>()).ResultsAsync;
            List<User> korisnici = rez.ToList();
            User korisnik = korisnici.First();
            return Ok(korisnik);

        }

        [HttpPost]
        [Route("dodaj")]
        public async Task<IActionResult> Dodaj([FromBody] User u)
        {

            var rez = await this.Neo.Cypher
                .Create("(n:User $korisnik)")
                .WithParam("korisnik", u)
                .Return(u => u.As<User>()).ResultsAsync;
                //.ExecuteWithoutResultsAsync();

            return Ok();
        }



        //Dictionary<string,string> param = new Dictionary<string,string>();
        //param["username"]
        /*
            +
            "{" +
            "UserName = $username," +
            "Name = $name" +
            "Mail = $mail" +
            "Password = $password" +
            "Salt = $salt" +
            "Description = $description" +
            "ProfilePicture = $profilepicture" +
            "Online = $online" +
            "PIN = $pin" +
            "})")
        */
    }
}
