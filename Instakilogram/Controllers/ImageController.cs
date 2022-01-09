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
using Instakilogram.Authentication;
using Instakilogram.RequestResponse;

namespace Instakilogram.Controllers
{
    [Auth]
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
            string mail = (string)HttpContext.Items["User"];

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
                        query.Set("p.description: $desc")
                            .WithParam("desc", request.Description);
                    }
                    if (request.TaggedUsers.Any())
                    {
                        foreach (string username in request.TaggedUsers)
                        {
                            if (this.Service.UserExists(username))
                            {
                                query.Match("(u:User)")
                                    //AndWhere umesto Where
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
                            query.Merge("(hTag:Hashtag {title: $new_title})")
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

        [HttpPost]
        [Route("ChangePhoto")]
        public async Task<IActionResult> ChangePhoto([FromBody] ChangePhotoRequest request)
        {
            string mail = (string)HttpContext.Items["User"];
            
            string picture_path = this.Service.ExtractPictureName(request.PictureURL);

            if (!this.Service.ImageCheck(mail, picture_path))
            {
                return BadRequest(new { message = "Slika ne postoji ili nije u vlasnistvu korisnika." });
            }

            if(!String.IsNullOrEmpty(request.Description))
            {
                await this.Neo.Cypher
                    .Match("(p:Photo {path: $photopath})")
                    .WithParam("photopath", picture_path)
                    .Set("p.description: $new")
                    .WithParam("new", request.Description)
                    .ExecuteWithoutResultsAsync();
            }
            if(request.Tags.Any())
            {
                await this.Neo.Cypher
                    .Match("(p:Photo {path: $photopath})-[r:TAGS]->(u:User)")
                    .WithParam("photopath", picture_path)
                    .Delete("r")
                    .ExecuteWithoutResultsAsync();

                foreach(string tag in request.Tags)
                {
                    await this.Neo.Cypher
                        .Match("(p:Photo {path: $photopath}),(u:User {userName: $name})")
                        .WithParam("photopath", picture_path)
                        .WithParam("name", tag)
                        .Create("(p)-[r:TAGS]->(u)")
                        .ExecuteWithoutResultsAsync();
                }
            }
            if(request.Hashtags.Any())
            {    
                List<string> exceptions = this.Service.CommonListElements(picture_path, request.Hashtags);
                this.Service.UpdateHashtags(picture_path, exceptions);

                foreach(string title in request.Hashtags)
                {
                    if(!exceptions.Contains(title))
                    {
                        Hashtag htag = this.Service.GetOrCreateHashtag(title);
                        await this.Neo.Cypher
                            .Match("(p:Photo {path: $path_val}), (h:Hashtag {title: $h_title})")
                            .WithParam("path_val", picture_path)
                            .WithParam("h_title", htag.Title)
                            .Create("(h)-[r:HAVE]->(p)")
                            .ExecuteWithoutResultsAsync();
                    }
                }
            }

            return Ok(new { message = "Uspesno promenjena slika." });            
        }

        [HttpDelete]
        [Route("DeletePhoto")]
        public async Task<IActionResult> DeletePhoto([FromBody] string picture_url)
        {
            string mail = (string)HttpContext.Items["User"];
            
            string picture_path = this.Service.ExtractPictureName(picture_url);

            if (!this.Service.ImageCheck(mail, picture_path))
            {
                return BadRequest(new { message = "Slika ne postoji ili nije u vlasnistvu korisnika." });
            }

            this.Service.UpdateHashtags(picture_path);

            await this.Neo.Cypher
                .Match("()-[r1]->(p:Photo {path: $photo_path}), (p:Photo)-[r2]->()")
                .WithParam("photo_path", picture_path)
                .Delete("r1")
                .Delete("r2")
                .ExecuteWithoutResultsAsync();

            await this.Neo.Cypher
                .Match("(p:Photo {path: $photo_path})")
                .WithParam("photo_path", picture_path)
                .Delete("p")
                .ExecuteWithoutResultsAsync();

            this.Service.DeleteImage(picture_path);

            return Ok(new { message = "Slika uspesno obrisana." });

        }

    }
}
