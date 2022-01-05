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

namespace Instakilogram.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ImageController : Controller
    {
        private IUserService Service;
        private IGraphClient Neo;

        public ImageController(IUserService service, IGraphClient gc)
        {
            this.Service = service;
            this.Neo = gc;
        }

        [HttpPost]
        [Route("AddPhoto")]
        public async Task<IActionResult> AddPhoto([FromForm] string? image_object, [FromForm] IFormFile Picture)
        {
            //cookie
            string mail = "andrija.djordjevic.97@gmail.com";
            //ovaj objekat (user) moze da se vrati iz cookie-a, tkd mozda ovaj deo sa obracanjem bazi nije potreban ovde
            User user = this.Neo.Cypher
                    .Match("(n:User)")
                    .Where((User n) => n.Mail == mail)
                    .Return(n => n.As<User>())
                    .ResultsAsync.Result.ToList().Single();

            if (user == null)
            {
                return BadRequest(new { message = "Korisnik ne postoji." });
            }

            //......

            if (Picture != null)
            {
                string path = this.Service.AddImage(Picture);
                Photo photo = new Photo {
                    Path = path,
                    TimePosted = DateTime.Now,
                    Description = null
                };

                await this.Neo.Cypher
                    .Match("(u:User)")
                    .Where((User n) => n.Mail == mail)
                    .Create("(p:Photo $prop)")
                    .WithParam("prop", photo)
                    .Create("(u)-[r:OWNS]->(p)")
                    .ExecuteWithoutResultsAsync();

                PhotoUpload request = JsonConvert.DeserializeObject<PhotoUpload>(image_object);
                if (request != null)
                {
                    Neo4jClient.Cypher.ICypherFluentQuery query = this.Neo.Cypher
                    .Match("(p:Photo)")
                    .Where((Photo p) => p.Path == photo.Path);

                    if (!String.IsNullOrEmpty(request.Description))
                    {
                        query.Set("p.description = $desc")
                            .WithParam("desc", request.Description);
                    }
                    if (request.TaggedUsers.Any())
                    {
                        foreach (string username in request.TaggedUsers)
                        {
                            if (this.Service.UserExists(username))
                            {
                                query.Match("(u:User)")
                                    .Where((User u) => u.UserName == username)
                                    .Create("(p)-[t:TAGS]->(u)");
                            }
                        }
                    }
                    if (request.Hashtags.Any())
                    {
                        foreach (string hTag in request.Hashtags)
                        {
                            //Hashtag tmpTag = this.Service.GetOrCreateHashtag(hTag);
                            query.Merge("(hTag:Hashtag {title = $new_title})")
                                .WithParam("new_title", hTag)
                                .Create("(hTag)-[h:HAVE]->(p)");
                            //proveriti da li je adekvatno napisan merge
                        }
                    }
                    await query.ExecuteWithoutResultsAsync();
                }
                return Ok(new { message = "Uspesno upload-ovana slika." });
            }
            else
            {
                return BadRequest(new { message = "Slika nije stigla." });
            }
        }







    }
}
