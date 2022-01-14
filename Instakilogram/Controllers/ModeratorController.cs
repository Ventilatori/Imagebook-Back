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
                string path = await this.Service.AddImage(ph);

            }
            return Ok();
        }
    }
}
