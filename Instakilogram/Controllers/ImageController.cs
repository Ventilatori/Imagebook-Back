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
using System.IO;
using StackExchange.Redis;

namespace Instakilogram.Controllers
{
    [Auth]
    [Route("[controller]")]
    [ApiController]
    public class ImageController : Controller
    {
        private IUserService Service;
        private IGraphClient Neo;
        private IConnectionMultiplexer Redis;

        public ImageController(IUserService service, IGraphClient gc, IConnectionMultiplexer redis)
        {
            this.Service = service;
            this.Neo = gc;
            this.Redis = redis;
        }

        [HttpPost]
        [Route("AddPhoto")]
        public async Task<IActionResult> AddPhoto([FromForm] PhotoUpload request /*string?  image_object, [FromForm] IFormFile Picture*/)
        {
            string mail = (string)HttpContext.Items["User"];

            if (request.Picture != null)
            {
                //
                ImageAsBase64 picture = new ImageAsBase64();

                if (request.Picture.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        request.Picture.CopyTo(ms);
                        var fileBytes = ms.ToArray();
                        picture.FileName = request.Picture.FileName;
                        picture.Base64Content = Convert.ToBase64String(fileBytes);
                    }
                }

                string path = this.Service.AddImage(picture);
                Photo photo = new Photo
                {
                    Path = path,
                    TimePosted = DateTime.Now,
                    Description = null
                };

                await this.Neo.Cypher
                    .Match("(u:User)")
                    .Where((User u) => u.Mail == mail)
                    .Create("(p:Photo $prop)")
                    .WithParam("prop", photo)
                    .Create("(u)-[r:UPLOADED]->(p)")
                    .ExecuteWithoutResultsAsync();


                if (request != null)
                {
                    if (!String.IsNullOrEmpty(request.Description))
                    {
                        /*var slicka = */
                        await this.Neo.Cypher
                        .Match("(p:Photo)")
                        .Where((Photo p) => p.Path == photo.Path)
                        .Set("p.Description = {desc}")
                        .WithParams(new { desc = request.Description })
                        //.Return<Photo>("p").ResultsAsync;
                        .ExecuteWithoutResultsAsync();
                    }
                    if (request.TaggedUsers != null)
                    {
                        foreach (string username in request.TaggedUsers)
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
                    if (request.Hashtags != null)
                    {
                        foreach (string hTag in request.Hashtags)
                        {
                            //Hashtag tmpTag = this.Service.GetOrCreateHashtag(hTag);



                            //query.Merge("(hTag:Hashtag {title: $new_title})")
                            //    .WithParam("new_title", hTag)
                            //    .Create("(hTag)-[h:HTAGS]->(p)");


                            //proveriti da li je adekvatno napisan merge
                        }
                    }

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
            //string mail = (string)HttpContext.Items["User"];

            //string picture_path = this.Service.ExtractPictureName(request.PictureURL);

            //if (!this.Service.ImageCheck(mail, picture_path))
            //{
            //    return BadRequest(new { message = "Slika ne postoji ili nije u vlasnistvu korisnika." });
            //}

            //if (!String.IsNullOrEmpty(request.Description))
            //{
            //    await this.Neo.Cypher
            //        .Match("(p:Photo {path: $photopath})")
            //        .WithParam("photopath", picture_path)
            //        .Set("p.description: $new")
            //        .WithParam("new", request.Description)
            //        .ExecuteWithoutResultsAsync();
            //}
            //if (request.Tags.Any())
            //{
            //    await this.Neo.Cypher
            //        .Match("(p:Photo {path: $photopath})-[r:TAGS]->(u:User)")
            //        .WithParam("photopath", picture_path)
            //        .Delete("r")
            //        .ExecuteWithoutResultsAsync();

            //    foreach (string tag in request.Tags)
            //    {
            //        await this.Neo.Cypher
            //            .Match("(p:Photo {path: $photopath}),(u:User {userName: $name})")
            //            .WithParam("photopath", picture_path)
            //            .WithParam("name", tag)
            //            .Create("(p)-[r:TAGS]->(u)")
            //            .ExecuteWithoutResultsAsync();
            //    }
            //}
            //if (request.Hashtags.Any())
            //{
            //    List<string> exceptions = this.Service.CommonListElements(picture_path, request.Hashtags);
            //    this.Service.UpdateHashtags(picture_path, exceptions);

            //    foreach (string title in request.Hashtags)
            //    {
            //        if (!exceptions.Contains(title))
            //        {
            //            Hashtag htag = this.Service.GetOrCreateHashtag(title);
            //            await this.Neo.Cypher
            //                .Match("(p:Photo {path: $path_val}), (h:Hashtag {title: $h_title})")
            //                .WithParam("path_val", picture_path)
            //                .WithParam("h_title", htag.Title)
            //                .Create("(h)-[r:HTAGS]->(p)")
            //                .ExecuteWithoutResultsAsync();
            //        }
            //    }
            //}

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

        //TODO FIX: Photo numberOfLikes has no limit per single caller
        [HttpPost]
        [Route("LikePhoto/{photofilename}")]
        public async Task<IActionResult> LikePhoto(string photofilename)
        {
            string Mail = (string)HttpContext.Items["User"];
            //check if like exists
            var query = await this.Neo.Cypher
             .Match("(a:User)-[r:LIKES]->(b:Photo)")
             .Where("a.Mail = $userA AND b.Path = $photoName")
             .WithParams(new { userA = Mail, photoName = photofilename })
              .Return<User>("a").ResultsAsync;
            if (query.Count() != 0)
                return Ok("vec ste lajkovali!");
            //create like
            await this.Neo.Cypher
                .Match("(a:User),(b:Photo)")
                .Where("a.Mail = $userA AND b.Path = $photoName")
                .WithParams(new { userA = Mail, photoName = photofilename })
                .Merge("(a)-[r:LIKES]->(b)")
                 .Set("b.NumberOfLikes = b.NumberOfLikes + 1")
                .ExecuteWithoutResultsAsync();


            return Ok();
        }

        [HttpDelete]
        [Route("UnlikePhoto/{photofilename}")]
        public async Task<IActionResult> UnlikePhoto(string photofilename)
        {
            string Mail = (string)HttpContext.Items["User"];

            await this.Neo.Cypher
                .Match("(a:User)-[r:LIKES]->(b:Photo)")
                .Where("a.Mail = $userA AND b.Path = $photoName")
                .WithParams(new { userA = Mail, photoName = photofilename })
                .Set("b.NumberOfLikes = b.NumberOfLikes - 1")
                .Delete("r")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpPost]
        [Route("AddPhotoToQueue")]
        public async Task<IActionResult> AddPhotoToQueue([FromForm] PhotoUpload request)
        {
           
            string Mail = (string)HttpContext.Items["User"];
            PhotoWithBase64 ph = new PhotoWithBase64();
            ph.Metadata.Path = request.Picture.FileName;
            ph.Metadata.Description = request.Description;
            ph.Metadata.TimePosted = DateTime.Now;
            ph.CallerEmail = Mail;
            ph.TaggedUsers = request.TaggedUsers;
            ph.Hashtags = request.Hashtags;

            var db = Redis.GetDatabase();
            if (request.Picture.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    request.Picture.CopyTo(ms);
                    var fileBytes = ms.ToArray();
                    string s = Convert.ToBase64String(fileBytes);
                    ph.Base64Content = s;
                    db.ListLeftPush("modqueue", JsonConvert.SerializeObject(ph));

                }
            }
            return Ok();
        }
    }
}
