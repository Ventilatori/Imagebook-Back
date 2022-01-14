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
        [Route("GetUnapprovedImage")]
        public async Task<IActionResult> GetUnapprovedPhotos()
        {
            //string Mail = (string)HttpContext.Items["User"];

            //var user_query = await this.Neo.Cypher
            //   .Match("(a:User)")
            //   .Where((User a) => a.Mail == Mail)
            //   .Return(a => a.As<User>())
            //   .ResultsAsync;

            //User user = user_query.Count() == 0 ? null : user_query.Single();


            //await this.Neo.Cypher
            //   .Match("(a:User)")
            //   .Where("a.Mail = $userA")
            //   .WithParams(new { userA = Mail });

            //User owner = qphotoOwner.Count() == 0 ? null : qphotoOwner.Single();


            var db = this.Redis.GetDatabase();
            PhotoWithBase64 pic = new PhotoWithBase64();
            if (db.KeyExists("modqueue"))
            {

                var image = db.ListRightPop("modqueue");
             
                 pic = JsonConvert.DeserializeObject<PhotoWithBase64>(image);
             
            }
            return Ok(pic);
        }

        [HttpPost]
        [Route("ApprovePhoto")]
        public async Task<IActionResult> ApprovePhoto([FromBody] PhotoWithBase64 photo)
        {
            
                await this.Service.AddImage(photo);

            
            return Ok();
        }
    }
}
