using Instakilogram.RequestResponse;
using Instakilogram.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Instakilogram.RequestResponse;
using StackExchange.Redis;
using System.IO;
using Instakilogram.Models;

namespace Instakilogram.Controllers
{

    [Route("[controller]")]
    [ApiController]
    public class ModeratorController : Controller
    {
        private IGraphClient Neo;
        public IHostingEnvironment hostingEnvironment;
        private IUserService Service;
        private IConnectionMultiplexer Redis;

        public ModeratorController(IGraphClient gc, IHostingEnvironment hostingEnv, IUserService service, IConnectionMultiplexer redis)
        {
            this.Neo = gc;
            hostingEnvironment = hostingEnv;
            Service = service;
            Redis = redis;
        }

        [HttpGet]
        [Route("GetUnapprovedImages")]
        public async Task<IActionResult> GetUnapprovedPhotos()
        {
            List<PhotoWithBase64> pics = new List<PhotoWithBase64>();
            var db = this.Redis.GetDatabase();
            if (db.KeyExists("modqueue"))
            {

                var images = db.ListRange("modqueue", 0, -1);
                foreach (var redisval in images)
                    pics.Add(JsonConvert.DeserializeObject<PhotoWithBase64>(redisval));
                db.KeyDelete("modqueue");
            }
            return Ok(pics);
        }

        [HttpPost]
        [Route("ApprovePhoto")]
        public async Task<IActionResult> ApprovePhoto([FromBody] List<PhotoWithBase64> photos)
        {
            foreach (PhotoWithBase64 ph in photos)
            {

                //add sliku u fajlsistem
                ImageAsBase64 picture = new ImageAsBase64 { Base64Content = ph.Base64Content,
                    CallerEmail = ph.CallerEmail, FileName = ph.Metadata.Path };
                string path = this.Service.AddImage(picture);
                //
                await this.Neo.Cypher
                    .Match("(u:User)")
                    .Where((User u) => u.Mail == ph.CallerEmail)
                    .Create("(p:Photo $prop)")
                    .WithParam("prop", ph.Metadata)
                    .Create("(u)-[r:UPLOADED]->(p)")
                    .ExecuteWithoutResultsAsync();

                if (!String.IsNullOrEmpty(ph.Metadata.Description))
                {
                    this.Neo.Cypher
                   .Match("(p:Photo)")
                   .Where((Photo p) => p.Path == ph.Metadata.Path)
                   .Set("p.Description = {desc}")
                   .WithParams(new { desc = ph.Metadata.Description });
                }
                if (ph.TaggedUsers != null)
                {
                    foreach (string username in ph.TaggedUsers)
                    {
                        if (this.Service.UserExists(username))
                        {
                            await this.Neo.Cypher
                                .Match("(u:User)")
                                .Where((User u) => u.UserName == username)
                                .Create("(p)-[t:TAGS]->(u)")
                                .ExecuteWithoutResultsAsync();
                        }
                    }
                }
                if (ph.Hashtags != null)
                {
                    foreach (string hTag in ph.Hashtags)
                    {
                      await this.Neo.Cypher
                        .Merge("(h:Hashtag {title: $new_title})")
                        .WithParam("new_title", hTag)
                        .Match("(a:User),(b:Hashtag)")
                        .Where("a.Mail = $userA AND b.title = $htitle")
                        .WithParams(new { userA = ph.CallerEmail, htitle = hTag })
                        .Merge("(a)-[r:HTAGS]->(b)")
                        .ExecuteWithoutResultsAsync();
                    }
                }
            }
            return Ok();

           
           

        }

         
        }
    
}
