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
using System.Text.RegularExpressions;
using Instakilogram.RequestResponse;

namespace Instakilogram.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class APIController : ControllerBase
    {
        private IGraphClient Neo;
        public IHostingEnvironment hostingEnvironment;
        private IUserService Service;

        public APIController(IGraphClient gc, IHostingEnvironment hostingEnv, IUserService service)
        {
            this.Neo = gc;
            hostingEnvironment = hostingEnv;
            Service = service;
        }

        [HttpGet]
        [Route("GetPhoto/{photoName}")]
        public async Task<IActionResult> GetPhoto(string photoName)
        {
            string Mail = (string)HttpContext.Items["User"];
            string picture = photoName;//this.Service.ExtractPictureName(photoName);

            var qphoto = await this.Neo.Cypher
                .Match("(p:Photo)")
                .Where((Photo p) => p.Path == picture)
                .Return(p => p.As<Photo>())
                .ResultsAsync;

            //Photo photo = qphoto.Count() == 0 ? null : qphoto.Single();
            Photo photo;

            if (qphoto.Count() == 0)
            {
                photo = null;
            }
            else
            {

                photo = qphoto.Single();
                if (photo != null)  photo = Service.ComputePhotoProp(Mail, photo);
            }


            var qphotoOwner = await this.Neo.Cypher
                .Match("(u:User)-[:UPLOADED]->(p:Photo{Path:$img_name})")
                .WithParam("img_name", picture)
                .Return(u => u.As<User>())
                .ResultsAsync;

            User owner = qphotoOwner.Count() == 0 ? null : qphotoOwner.Single();



            var qtusers = await this.Neo.Cypher
               .Match("(u:User)<-[:TAGS]-(p:Photo{Path:$img_name})")
               .WithParam("img_name", picture)
               .Return(u => u.CollectAs<User>())
               .ResultsAsync;

            List<User> tUsers = qtusers.Count() == 0 ? null : qtusers.ToList().Single().ToList();


            var qhtags = await this.Neo.Cypher
                .Match("(h:Hashtag)-[:HTAGS]->(p:Photo{Path:$img_name})")
                .WithParam("img_name", picture)
                .Return(h => h.CollectAs<Hashtag>())
                .ResultsAsync;

            List<Hashtag> htags = qhtags.Count() == 0 ? null : qhtags.ToList().Single().ToList();

            return Ok(new { Photo = photo, User = owner, Hashtags = htags, TaggedUsers = tUsers });

        }

        [HttpGet]
        [Route("GetFeed24h")] //return photo and user in list 
        public async Task<IActionResult> GetFeed24h()
        {
            string Mail = (string)HttpContext.Items["User"];

            var usersFollowed = await this.Neo.Cypher
                .Match("(a:User)-[:FOLLOWS]->(b:User)")
                .Where((User a) => a.Mail == Mail)
                .Return<User>("b").ResultsAsync;

            var photos = new List<Photo>();
            foreach (User u in usersFollowed)
            {

                DateTime now = DateTime.Now;
                var phList = await this.Neo.Cypher
                    .Match("(a:User{UserName:$nameParam})-[:UPLOADED]->(p:Photo)")
                    .WithParam("nameParam", u.UserName)
                    .Return<Photo>("p").ResultsAsync;

                var photolist = phList.ToList<Photo>();
                for (int i = 0; i < photolist.Count(); i++)
                {
                    if (Service.IsFromLast24h(photolist[i].TimePosted))
                    {
                        photolist[i] = Service.ComputePhotoProp(Mail, photolist[i]);
                        var ph = photolist[i];
                        photos.Add(ph);
                    }
                }
                
                    
            }
            return Ok(photos);
        }


        [HttpPost]
        [Route("FollowUser/{usernameToFollow}")]
        public async Task<IActionResult> FollowUser(string usernameToFollow)
        {
            string Mail = (string)HttpContext.Items["User"];

            await this.Neo.Cypher
                .Match("(a:User),(b:User)")
                .Where("a.Mail = $userA AND b.UserName = $userB")
                .WithParams(new { userA = Mail, userB = usernameToFollow })
                .Create("(a)-[r:FOLLOWS]->(b)")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpDelete]
        [Route("UnfollowUser/{usernameToUnfollow}")]
        public async Task<IActionResult> UnfollowUser(string usernameToUnfollow)
        {
            string Mail = (string)HttpContext.Items["User"];
            await this.Neo.Cypher
                .Match("(a:User)-[r:FOLLOWS]->(b:User)")
                .Where("a.Mail = $userA AND b.UserName = $userB")
                .WithParams(new { userA = Mail, userB = usernameToUnfollow })
                .Delete("r")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpPost]
        [Route("FollowHashtag/{hashtagToFollow}")]
        public async Task<IActionResult> FollowHashtag(string hashtagToFollow)
        {
            string Mail = (string)HttpContext.Items["User"];

            await this.Neo.Cypher
                .Match("(a:User),(b:Hashtag)")
                .Where("a.Mail = $userA AND b.Title = $hashtagB")
                .WithParams(new { userA = Mail, hashtagB = hashtagToFollow })
                .Create("(a)-[r:FOLLOWS]->(b)")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpDelete]
        [Route("UnfollowHashtag/{hashtagToUnfollow}")]
        public async Task<IActionResult> Unfollow(string hashtagToUnfollow)
        {
            string Mail = (string)HttpContext.Items["User"];

            await this.Neo.Cypher
                .Match("(a:User)-[r:FOLLOWS]->(b:Hashtag)")
                .Where("a.Mail = $userA AND b.Title = $hashtagB")
                .WithParams(new { userA = Mail, hashtagB = hashtagToUnfollow })
                .Delete("r")
                .ExecuteWithoutResultsAsync();
            return Ok();
        }

        [HttpPost]
        [Route("UploadProfilePic")]
        public async Task<IActionResult> UploadProfilePic(IFormFile file)
        {
            string Mail = (string)HttpContext.Items["User"];

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

        [HttpGet]
        [Route("Search/{username}")]
        public async Task<IActionResult> Search(string username)
        {
            Regex rgx = new Regex("" + username + ".*");

            var matchingUsers = await this.Neo.Cypher
               .Match("(a:User)")
               .Where((User a) => a.UserName.Contains(username))
               .Return<User>("a").ResultsAsync;
            return Ok(matchingUsers);
        }

        [HttpGet]
        [Route("GetRecommendedUsers")]
        public async Task<IActionResult> GetRecommendedUsers()
        {
            string Mail = (string)HttpContext.Items["User"];

            int minimumConnectedPeople = 2;

            Dictionary<User, int> peopleToRecommend = new Dictionary<User, int>();

            var myFriendList = await this.Neo.Cypher
              .Match("(a:User)-[:FOLLOWS]->(b:User)")
              .Where((User a) => a.Mail == Mail)
              .Return<User>("b").ResultsAsync;

            foreach (User friend in myFriendList)
            {
                var friend_friendList = await this.Neo.Cypher
                    .Match("(a:User)-[:FOLLOWS]->(b:User)")
                    .Where((User a) => a.UserName == friend.UserName)
                    .Return<User>("b").ResultsAsync;

                foreach (User friendOfFriend in friend_friendList)
                {
                    if (!myFriendList.Contains(friendOfFriend))
                    {
                        peopleToRecommend[friendOfFriend]++;
                    }
                }
            }

            var matches = peopleToRecommend.Where(kvp => kvp.Value > minimumConnectedPeople);

            return Ok(matches);
        }

        [HttpGet]
        [Route("GetUser/{userName}")]
        public async Task<IActionResult> GetUser(string userName)
        {
            string Mail = (string)HttpContext.Items["User"];

            var user_query = await this.Neo.Cypher
                .Match("(a:User)")
                .Where((User a) => a.UserName == userName)
                //.Return<User>("a")
                .Return(a => a.As<User>())
                .ResultsAsync;

            User user = user_query.Count() == 0 ? null : user_query.Single();

            var photos_query = await this.Neo.Cypher
               .Match("(a:User{UserName:$nameParam})-[:UPLOADED]->(p:Photo)")
               .WithParam("nameParam", userName)
               //.Return<Photo>("p")
               .Return(p => p.CollectAs<Photo>())
               .ResultsAsync;
            List<Photo> uploadedPhotos = photos_query.Count() == 0 ? null : photos_query.ToList().Single().ToList();

            if (uploadedPhotos != null)
            {
                for (int i = 0; i < uploadedPhotos.Count(); i++)
                {
                    uploadedPhotos[i] = Service.ComputePhotoProp(Mail, uploadedPhotos[i]);
                }
                foreach (Photo pp in uploadedPhotos)
                {
                   
                }
            }

            var taggedOnPhotos_query = await this.Neo.Cypher
                .Match("(p:Photo)-[:TAGS]->(a:User{UserName:$nameParam})")
                .WithParam("nameParam", userName)
                //.Return<Photo>("p")
                .Return(p => p.CollectAs<Photo>())
                .ResultsAsync;
            List<Photo> taggedOnPhotos = taggedOnPhotos_query.Count() == 0 ? null : taggedOnPhotos_query.ToList().Single().ToList();

            if (taggedOnPhotos != null)
            {
                for (int i = 0; i < taggedOnPhotos.Count(); i++)
                {
                    taggedOnPhotos[i] = Service.ComputePhotoProp(Mail, taggedOnPhotos[i]); ;
                }
             
            }

            return Ok(new GetUserResponse
            {
                User = user,
                UploadedPhotos = uploadedPhotos,
                TaggedPhotos = taggedOnPhotos
            });
        }

        //[HttpGet]
        //[Route("getphotoproto/{path}")]
        //public async Task<IActionResult> getphotoproto(string path)
        //{
        //    string Mail = (string)HttpContext.Items["User"];




        //    var qphoto = await this.Neo.Cypher
        //        .Match("(p:Photo)")
        //        .Where((Photo p) => p.Path == ph.path)
        //        .Return(p => p.As<Photo>())
        //        .ResultsAsync;

        //    if (qphoto.Count() == 0)
        //    {
        //        return Ok("Nema slicke");
        //    }

        //    Photo photo = qphoto.Single();
           
        //    return Ok(photo);
        //}
    }
}
