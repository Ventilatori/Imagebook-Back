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
        private readonly Moderation _moderation;
        private IUserService Service;
        private IGraphClient Neo;
        private IConnectionMultiplexer Redis;

        public ImageController(IUserService service, IGraphClient gc, IConnectionMultiplexer redis, Moderation moderation)
        {
            this._moderation = moderation;
            this.Service = service;
            this.Neo = gc;
            this.Redis = redis;
        }

        [HttpPut]
        [Route("UpdateDesc/{filename}")]
        public async Task<IActionResult> UpdateDesc(string filename, [FromBody] string newDesc)
        {
            if (!String.IsNullOrEmpty(newDesc))
            {
                await this.Neo.Cypher
                    .Match("(p:Photo{Path:$photopath})")
                    .WithParam("photopath", filename)
                    .Set("p.Description=$new")
                    .WithParam("new", newDesc)
                    .ExecuteWithoutResultsAsync();
            }
            return Ok();
        }
        [HttpPut]
        [Route("UpdateHtags/{filename}")]
        public async Task<IActionResult> UpdateHtags(string filename, [FromBody] string newHtags)
        {
            //todo
            //validiraj dal postoji stvarno taj htag
            //promeni relaciju u neo4j

            var qhtags = await this.Neo.Cypher
                .Match("(h:Hashtag)-[:HTAGS]->(p:Photo{Path:$img_name})")
                .WithParam("img_name", filename)
                .Return(h => h.CollectAs<Hashtag>())
                .ResultsAsync;

            List<Hashtag> existingHtags = qhtags.Count() == 0 ? null : qhtags.ToList().Single().ToList();

            if (newHtags != null)
            {

                string[] newHtagsArray = newHtags.Split('|');

                if (existingHtags != null)
                {
                    foreach (Hashtag h in existingHtags)
                    {
                        if(!newHtagsArray.Contains(h.Title))
                        await this.Neo.Cypher
                         .Match("(h:Hashtag{Title:$htitle})-[r:HTAGS]->(p:Photo{Path:$img_name})")
                         .WithParams(new { htitle = h.Title, img_name = filename })
                         .Delete("r")
                         .ExecuteWithoutResultsAsync();
                    }
                }

                foreach (string hTag in newHtags.Split('|'))
                {
                    await this.Neo.Cypher
                        .Merge("(h:Hashtag {Title: $new_title})")
                        .WithParam("new_title", hTag)
                        .With("h as hh")

                        .Match("(p:Photo)")
                        .Where("p.Path = $path ")
                        .WithParams(new { title = hTag, path = filename })
                        .Create("(hh)-[s:HTAGS]->(p)")
                        .ExecuteWithoutResultsAsync();
                }
            }
            return Ok();
        }
        [HttpPut]
        [Route("UpdateTaggedP/{filename}")]
        public async Task<IActionResult> UpdateTaggedP(string filename, [FromBody] string newTaggedP)
        {
            var qtusers = await this.Neo.Cypher
              .Match("(u:User)<-[:TAGS]-(p:Photo{Path:$img_name})")
              .WithParam("img_name", filename)
              .Return(u => u.CollectAs<User>())
              .ResultsAsync;

            List<User> existingUsers = qtusers.Count() == 0 ? null : qtusers.ToList().Single().ToList();

            if (newTaggedP != null)
            {

                string[] newTaggedPArray = newTaggedP.Split('|');

                if (newTaggedP != null)
                {
                    if (existingUsers != null)
                    {
                        foreach (User u in existingUsers)
                        {
                            if (!newTaggedPArray.Contains(u.UserName))
                                await this.Neo.Cypher
                                 .Match("(p:Photo{Path:$img_name})-[r:TAGS]->(u:User{UserName:$uname})")
                                 .WithParams(new { img_name = filename, uname = u.UserName })
                                 .Delete("r")
                                 .ExecuteWithoutResultsAsync();
                        }
                    }

                    foreach (string username in newTaggedPArray)
                    {
                        if (this.Service.UserExists(username))
                        {
                            await this.Neo.Cypher
                                .Match("(u:User), (p:Photo)")
                                .Where("u.UserName = $usr AND p.Path = $path")
                                .WithParams(new { usr = username, path = filename })
                                .Merge("(p)-[t:TAGS]->(u)")
                                .ExecuteWithoutResultsAsync();
                        }
                    }

                }
               
               
            }
            return Ok();
        }
        [HttpPut]
        [Route("UpdateTitle/{filename}")]
        public async Task<IActionResult> UpdateTitle(string filename, [FromBody] string newTitle)
        {
            if (!String.IsNullOrEmpty(newTitle))
            {
                await this.Neo.Cypher
                    .Match("(p:Photo{Path:$photopath})")
                    .WithParam("photopath", filename)
                    .Set("p.Title=$new")
                    .WithParam("new", newTitle)
                    .ExecuteWithoutResultsAsync();
            }
            return Ok();
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
        public async Task<IActionResult> DeletePhoto( )
        {
            //todo edit string
            string picture_url = "";
    

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
        [Route("AddPhoto")]
        public async Task<IActionResult> AddPhoto([FromForm] PhotoUpload request)
        {
           

            string Mail = (string)HttpContext.Items["User"];
            PhotoWithBase64 ph = new PhotoWithBase64();
            ph.Metadata = new Photo();
            ph.Metadata.Path = request.Picture.FileName;
            ph.Metadata.Description = request.Description;
            ph.Metadata.TimePosted = DateTime.Now;
            ph.Metadata.Title = request.Title;
            ph.CallerEmail = Mail;
            ph.Metadata.TaggedUsers = request.TaggedUsers;
            ph.Metadata.Hashtags = request.Hashtags;
            

            using (var ms = new MemoryStream())
            {
                request.Picture.CopyTo(ms);
                var fileBytes = ms.ToArray();
                string s = Convert.ToBase64String(fileBytes);
                ph.Base64Content = s;
            }

            if (_moderation.IsOn)
            {
                var db = Redis.GetDatabase();
                if (request.Picture.Length > 0)
                {
                    db.ListLeftPush("modqueue", JsonConvert.SerializeObject(ph));
                }
                return Ok(new { Message = "Moderation is on" });
            }
            else
            {

               await this.Service.AddImage(ph);
                return Ok(new { Message = "Moderation is off" });
            }
            

        }
    }
}
