using Instakilogram.Models;
using Instakilogram.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Instakilogram.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Instakilogram.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class APIController : ControllerBase
    {
        private IGraphClient Neo;
        private readonly IDriver _driver;
        public  IHostingEnvironment hostingEnvironment;
        private IUserService Service;



        //private IUserService Service;
        public APIController(IGraphClient gc, IHostingEnvironment hostingEnv, IUserService service)
        {
            this.Neo = gc;
            hostingEnvironment = hostingEnv;
            Service = service;
        }

        // [HttpGet]
        // [Route("preuzmi")]
        // public async Task<IActionResult> Preuzmi()
        // {
        //     var rez = await this.Neo.Cypher
        //         .Match("(n:User)")
        //         .Return<User>("n").ResultsAsync;
        //     List<User> korisnici = rez.ToList();
        //     User korisnik = korisnici.First();
        //     return Ok(rez);
        // }

        // [HttpPost]
        // [Route("dodaj")]
        // public async Task<IActionResult> Dodaj([FromBody] User u)
        // {

        //     var rez = await this.Neo.Cypher
        //         .Create("(n:User $korisnik)")
        //         .WithParam("korisnik", u)
        //         .Return(u => u.As<User>()).ResultsAsync;
        //     //.ExecuteWithoutResultsAsync();

        //     return Ok();
        // }

        //nisam siguran sto se konverzije u rez tice, prepravicu ovo
        //[HttpGet]
        //[Route("GetProfile/{username}")]
        //public async Task<IActionResult> GetProfile(string username)
        //{
        //    var rez = this.Neo.Cypher
        //        .Match("(u:User)-[:UPLOADED]->(p:Photo)")
        //        .Where((User u)=> u.userName == username)
        //        .Return((User u, Photo p) => new{ user = u.As<User>(), photos = p.CollectAs<Photo>() } )
        //        .ResultsAsync.Result.Single();
        //}

        [HttpGet]
        [Route("GetFeed24h/{callerUsername}")] //
        public async Task<IActionResult> GetFeed24h(string callerUsername)
        {
            var usersFollowed = await this.Neo.Cypher
                .Match("(a:User)-[:FOLLOWS]->(b:User)")
                .Where((User a) => a.UserName == callerUsername)
                .Return<User>("b").ResultsAsync;

            var photos = new List<Photo>();
            foreach (User u in usersFollowed)
            {
               
                DateTime now = DateTime.Now;
                var phList = await this.Neo.Cypher
                    .Match("(a:User{UserName:$nameParam})-[:UPLOADED]->(p:Photo)")
                    .WithParam("nameParam", u.UserName)
                    .Return<Photo>("p").ResultsAsync;
                foreach (Photo pp in phList)
                    if(Service.IsFromLast24h(pp.TimePosted))
                        photos.Add(pp);
            }
            return Ok(photos);
        }

        [HttpPost]
        [Route("FollowUser/{callerUsername}/{usernameToFollow}")] 
        public async Task<IActionResult> FollowUser(string callerUsername, string usernameToFollow)
        {
            await this.Neo.Cypher
                .Match("(a:User),(b:User)")
                .Where("a.UserName = $userA AND b.UserName = $userB")
                .WithParams(new { userA = callerUsername, userB = usernameToFollow })
                .Create("(a)-[r:FOLLOWS]->(b)")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpDelete]
        [Route("UnfollowUser/{callerUsername}/{usernameToUnfollow}")] 
        public async Task<IActionResult> UnfollowUser(string callerUsername, string usernameToUnfollow)
        {
            await this.Neo.Cypher
                .Match("(a:User)-[r:FOLLOWS]->(b:User)")
                .Where("a.UserName = $userA AND b.UserName = $userB")
                .WithParams(new { userA = callerUsername, userB = usernameToUnfollow })
                .Delete("r")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpPost]
        [Route("FollowHashtag/{callerUsername}/{hashtagToFollow}")] 
        public async Task<IActionResult> FollowHashtag(string callerUsername, string hashtagToFollow)
        {
            await this.Neo.Cypher
                .Match("(a:User),(b:Hashtag)")
                .Where("a.UserName = $userA AND b.Title = $hashtagB")
                .WithParams(new { userA = callerUsername, hashtagB = hashtagToFollow })
                .Create("(a)-[r:FOLLOWS]->(b)")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpDelete]
        [Route("UnfollowHashtag/{callerUsername}/{hashtagToUnfollow}")] 
        public async Task<IActionResult> Unfollow(string callerUsername, string hashtagToUnfollow)
        {
            await this.Neo.Cypher
                .Match("(a:User)-[r:FOLLOWS]->(b:Hashtag)")
                .Where("a.UserName = $userA AND b.Title = $hashtagB")
                .WithParams(new { userA = callerUsername, hashtagB = hashtagToUnfollow })
                .Delete("r")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }


        [HttpPost]
        [Route("UploadProfilePic/{callerUsername}")]
        public async Task<IActionResult> UploadProfilePic(string callerUsername, IFormFile file)
        {
            if (!file.ContentType.Contains("image"))
            {
                return Ok("bad image");
            }
          
            var extension = "." + file.FileName.Split('.')[file.FileName.Split('.').Length - 1];
            string fileName = DateTime.Now.Ticks + extension; //Create a new Name for the file due to security reasons.

            var pathBuilt = Path.Combine(Directory.GetCurrentDirectory(), "Upload\\callerUsername\\profilepics");

            if (!Directory.Exists(pathBuilt))
            {
                Directory.CreateDirectory(pathBuilt);
            }

            var path = Path.Combine(Directory.GetCurrentDirectory(), "Upload\\callerUsername\\profilepics",
                fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok();
        }
        //[HttpPost]
        //[Route("LikePhoto/{callerUsername}/{photoID}")]
        //public async Task<IActionResult> LikePhoto(string callerUsername, string photoID)
        //{
        //    await this.Neo.Cypher
        //        .Match("(a:User),(b:Hashtag)")
        //        .Where("a.UserName = $userA AND b.Title = $hashtagB")
        //        .WithParams(new { userA = callerUsername, hashtagB = hashtagToFollow })
        //        .Create("(a)-[r:FOLLOWS]->(b)")
        //        .ExecuteWithoutResultsAsync();
        //    return Ok();
        //}


    }
}
