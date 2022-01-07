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
            string mail = HttpContext.Items["User"];

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
        [Route("DescriptionChange")]
        public async Task<IActionResult> ChangePhotoDescription([FromBody] string picture_url, [FromBody] string new_description)
        {
            string mail = HttpContext.Items["User"];

            string picture_path = this.Service.ExtractPictureName(picture_url);

            //ovo mozda izazove gresku ako nema slike (napisati koristeci prvo proveru da li slika postoji - Service.CheckImage - pa tek onda Set())
            Photo p = this.Neo.Cypher
                .Match("(u:User {mail: $email})-[:OWNS]->(p:Photo {path: $photopath})")
                .WithParam("email", mail)
                .WithParam("photopath", picture_path)
                .Set("p.description: $new")
                .WithParam("new", new_description)
                .Return(p => p.As<Photo>())
                .ResultsAsync.Result.ToList().Single();

            if (p == null)
            {
                return BadRequest(new { message = "Slika ne postoji." });
            }
            else
            {
                return Ok(new { message = "Uspesno promenjen opis slike." });
            }
        }

        //tagovi se salju kao lista da bi se azurirali (salje se cela nova lista tagova)
        [HttpPost]
        [Route("ChangeTags")]
        public async Task<IActionResult> ChangeTaggedUsersOnPhoto([FromBody] string picture_url, [FromBody] string new_tag_list)
        {
            string mail = HttpContext.Items["User"];

            List<string> tags = JsonConvert.DeserializeObject<List<string>>(new_tag_list);
            if (!tags.Any())
            {
                return BadRequest(new { message = "Lista tagova prazna." });
            }

            string picture_path = this.Service.ExtractPictureName(picture_url);

            if (!this.Service.ImageCheck(mail, picture_path))
            {
                return BadRequest(new { message = "Slika ne postoji ili nije u vlasnistvu korisnika." });
            }

            await this.Neo.Cypher
                .Match("(p:Photo {path: $photopath})-[r:TAGS]->(u:User)")
                .WithParam("photopath", picture_path)
                .Delete("r")
                .ExecuteWithoutResultsAsync();

            foreach(string tag in tags)
            {
                await this.Neo.Cypher
                    .Match("(p:Photo {path: $photopath}),(u:User {userName: $name})")
                    .WithParam("photopath", picture_path)
                    .WithParam("name", tag)
                    .Create("(p)-[r:TAGS]->(u)")
                    .ExecuteWithoutResultsAsync();
            }

            return Ok(new { message = "Tagovi azurirani." });
        }

        [HttpPost]
        [Route("ChangeHashtags")]
        public async Task<IActionResult> ChangePhotoHashtags([FromBody] string picture_url, [FromBody] string new_hashtag_list)
        {
            string mail = HttpContext.Items["User"];

            List<string> tags = JsonConvert.DeserializeObject<List<string>>(new_hashtag_list);
            if (!tags.Any())
            {
                return BadRequest(new { message = "Lista hashtagova prazna." });
            }

            string picture_path = this.Service.ExtractPictureName(picture_url);

            if (!this.Service.ImageCheck(mail, picture_path))
            {
                return BadRequest(new { message = "Slika ne postoji ili nije u vlasnistvu korisnika." });
            }

            //ovo gore sve moze da se ubaci u 1 Service f-ju koja ce da vraca string (message) koja ako je prazna sve je ok, ako nije vraca se BadRequest

            //sve relacije izmedju slika i hashtagova se obrisu
            //svi ti hashtagovi se stave u listu da bi se mogli obraditi (obrisati) ukoliko nema vise slika na njima
            //i da li je uklonjena profilna slika sa hashtaga (zbog azuriranja profilne hashtaga) (optional)

            this.Service.UpdateHashtags(picture_path);

            //obnove se sve veze sa hashtagovima u bazi na osnovu prosledjene liste

            foreach(string title in tags)
            {
                Hashtag htag = this.Service.GetOrCreateHashtag(title);
                await this.Neo.Cypher
                    .Match("(p:Photo {path: $path_val}), (h:Hashtag {title: $h_title})")
                    .WithParam("path_val", picture_path)
                    .WithParam("h_title", htag.Title)
                    .Create("(h)-[r:HAVE]->(p)")
                    .ExecuteWithoutResultsAsync();
            }

            return Ok(new { message = "Hashtagovi azurirani." });
        }

        [HttpDelete]
        [Route("DeletePhoto")]
        public async Task<IActionResult> DeletePhoto([FromBody] string picture_url)
        {
            string mail = HttpContext.Items["User"];
            
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
