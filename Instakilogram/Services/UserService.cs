using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Neo4jClient;
using Instakilogram.Models;
using StackExchange.Redis;
using Newtonsoft.Json;
using Instakilogram.RequestResponse;
using Instakilogram.Services;

namespace Instakilogram.Service
{
    public interface IUserService
    {
        enum MailType
        {
            Verify,
            ResetPassword
        };
        public enum ImageType
        {
            Standard,
            Profile
        };
        string AddImage(ImageAsBase64 picture, ImageType img_type = ImageType.Standard);
        bool DeleteImage(string picture_path, ImageType img_type = ImageType.Standard);
        bool ImageCheck(string mail, string picture_path);
        int PinGenerator();
        void SavePin(string mail, int PIN);
        bool CheckPin(string mail, int new_pin);
        bool CheckPassword(string hash_string, string salt_string, string password_string);
        void PasswordHash(out string hash_string, out string salt_string, string password_string);
        void SendMail(User user, MailType type);
        bool UserExists(string new_user_name, string new_mail = "");
        void TmpStoreAccount(User user, IFormFile Picture = null);
        string ApproveAccount(string key);
        User GetUser(string username);
        Hashtag GetOrCreateHashtag(string title);
        string ExtractPictureName(string url);
        List<string> CommonListElements(string picture_path, List<string> new_hashtags);
        void UpdateHashtags(string picture_path, List<string> exceptions=null);
        string GenerateCookie(int length = 25);
        void StoreCookie(string key, string mail);
        string? CheckCookie(string key);
        void DeleteCookie(string key);
        ImageAsBase64 FormFileToBase64(IFormFile ff);
        bool IsFromLast24h(DateTime timeForChecking);
    }

    public class UserService : IUserService
    {
        public MailSettings _mailSettings { get; set; }
        public IWebHostEnvironment Environment { get; set; }
        public IGraphClient Neo;
        public IConnectionMultiplexer Redis;
        public URLs URL { get; set; }

        public UserService(IGraphClient gc, IConnectionMultiplexer mux, IOptions<MailSettings> mailSettings, IOptions<URLs> url, IWebHostEnvironment environment) {
            this.Neo = gc;
            this._mailSettings = mailSettings.Value;
            this.URL = url.Value;
            this.Environment = environment;
            this.Redis = mux;
        }
        public string AddImage(ImageAsBase64 picture, IUserService.ImageType img_type = IUserService.ImageType.Standard)
        {
            string folderPath = "Images\\"+img_type.ToString();
            string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
            string file_name; //
            if (picture != null)
            {
                file_name = Guid.NewGuid().ToString() + "_" + picture.FileName;
                string filePath = Path.Combine(uploadsFolder, file_name);
                File.WriteAllBytes(filePath, Convert.FromBase64String(picture.Base64Content));
            }
            else
            {
                file_name = "default.png";
            }
            return file_name;
        }
        public bool DeleteImage(string picture_name, IUserService.ImageType img_type = IUserService.ImageType.Standard)
        {
            if (!String.Equals(picture_name, "default.png"))
            {
                string folderPath = "Images\\"+img_type.ToString();
                
                string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
                string filePath = Path.Combine(uploadsFolder, picture_name);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    return true;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }
        public bool ImageCheck(string mail, string picture_path)
        {
            Photo p = this.Neo.Cypher
                .Match("(u:User {mail: $email})-[:UPLOADED]->(p:Photo {path: $photopath})")
                .WithParam("email", mail)
                .WithParam("photopath", picture_path)
                .Return(p => p.As<Photo>())
                .ResultsAsync.Result.ToList().Single();
            return p == null ? false : true;
        }
        public ImageAsBase64 FormFileToBase64(IFormFile ff)
        {
            ImageAsBase64 result = new ImageAsBase64();
            if (ff.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    ff.CopyTo(ms);
                    var fileBytes = ms.ToArray();
                    result.FileName = ff.FileName;
                    result.Base64Content = Convert.ToBase64String(fileBytes);
                    // act on the Base64 data
                }
            }
            return result;
        }
        public bool CheckPassword(string hash_string, string salt_string, string password_string)
        {
            byte[] salt = Encoding.UTF8.GetBytes(salt_string);
            byte[] valid_hash = Encoding.UTF8.GetBytes(hash_string);
            HMACSHA512 hashObj = new HMACSHA512(salt);
            byte[] password = Encoding.UTF8.GetBytes(password_string);
            byte[] computed_hash = hashObj.ComputeHash(password);

            int len = computed_hash.Length;
            for (int i = 0; i < len; i++)
            {
                if (valid_hash[i] != computed_hash[i])
                {
                    return false;
                }
            }
            return true;
        }
        public void PasswordHash(out string hash_string, out string salt_string, string password_string)
        {
            byte[] hash,salt;
            //PasswordHasher hashObj = new PasswordHasher(this);
            HMACSHA512 hashObj = new HMACSHA512();
            salt = hashObj.Key;
            byte[] password = Encoding.UTF8.GetBytes(password_string);
            hash = hashObj.ComputeHash(password);
            hash_string = Encoding.UTF8.GetString(hash.ToArray());
            salt_string = Encoding.UTF8.GetString(salt.ToArray());
        }

        public void SendMail(User user, IUserService.MailType type)
        {
            MailMessage msg = new MailMessage();
            msg.From = new MailAddress(_mailSettings.Address, _mailSettings.Name);
            msg.To.Add(user.Mail);
            msg.Subject = _mailSettings.Subject;

            if (type == IUserService.MailType.Verify)
            {
                string path = Path.Combine(Environment.WebRootPath, "Mail\\Verify.html");
                string text = System.IO.File.ReadAllText(path);
                text = text.Replace("~", URL.VerifyURL + user.UserName);
                msg.Body = text;
            }
            else if (type == IUserService.MailType.ResetPassword)
            {
                string path = Path.Combine(Environment.WebRootPath, "Mail\\ResetPassword.html");
                string text = System.IO.File.ReadAllText(path);
                int PIN = PinGenerator();
                this.SavePin(user.Mail, PIN);
                text = text.Replace("`", PIN.ToString());
                text = text.Replace("~", this.URL.PasswordResetURL);
                msg.Body = text;
            }

            msg.IsBodyHtml = true;

            var smtpClient = new SmtpClient("smtp.gmail.com");
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(_mailSettings.Address, _mailSettings.Password);
            smtpClient.Port = 587;
            smtpClient.EnableSsl = true;
            smtpClient.Send(msg);

        }
        public int PinGenerator()
        {
            int _min = 1000;
            int _max = 9999;
            Random _rdm = new Random();
            return _rdm.Next(_min, _max);
        }
        public void SavePin(string mail, int PIN)
        {
            DateTime d1 = DateTime.Now;
            DateTime d2 = d1.AddDays(1);
            TimeSpan t = d2 - d1;
            var db = this.Redis.GetDatabase();
            db.StringSetAsync(mail, PIN, t);
        }
        public bool CheckPin(string mail, int new_pin)
        {
            var db = this.Redis.GetDatabase();
            int? saved_pin = Int32.Parse(db.StringGetAsync(mail).Result);
            return saved_pin != null && new_pin == saved_pin ? true : false;
        }

        public bool UserExists(string new_user_name, string new_mail = "")
        {
            var query = this.Neo.Cypher
                .Match("(u:User)")
                .Where((User u) => u.UserName == new_user_name || u.Mail == new_mail)
                .Return(u => u.As<User>())
                .ResultsAsync.Result;
            //User result = query.ToList().Single();
            if(query.Any())
            {
                return true;
            }
            
            var db = this.Redis.GetDatabase();
            //var _result = db.StringGetAsync(new_user_name).Result;
            if(db.KeyExists(new_user_name))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void TmpStoreAccount(User user, IFormFile Picture = null)
        {
            var db = this.Redis.GetDatabase();
            string user_string = JsonConvert.SerializeObject(user);
            DateTime d1 = DateTime.Now;
            DateTime d2 = d1.AddDays(1);
            TimeSpan t = d2 - d1;
            db.StringSetAsync(user.UserName, user_string, t);

            if(Picture != null)
            {
                //
                if (Picture.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        Picture.CopyTo(ms);
                        var fileBytes = ms.ToArray();
                        string s = Convert.ToBase64String(fileBytes);

                        ImageAsBase64 pic = new ImageAsBase64();
                        pic.FileName = Picture.FileName;
                        pic.Base64Content = s;


;                      
                        db.StringSetAsync(user.UserName + "Profile", JsonConvert.SerializeObject(pic), t);
                        
                    }
                }
                //
                //string img_string = JsonConvert.SerializeObject(Picture);
                //db.StringSetAsync(user.UserName + "Profile", img_string, t);
            }
            
            this.SendMail(user, IUserService.MailType.Verify);
        }
        public string ApproveAccount(string key)
        {
            string link = null;
            var db = this.Redis.GetDatabase();
            if(db.KeyExists(key))
            {
                var result = db.StringGetAsync(key).Result;
                User user = JsonConvert.DeserializeObject<User>(result);
                string img_key = user.UserName + "Profile";
                if (String.Equals(user.ProfilePicture,"") && db.KeyExists(img_key))
                {
                    var img_string = db.StringGetAsync(img_key).Result;
                    ImageAsBase64 pic = JsonConvert.DeserializeObject<ImageAsBase64>(img_string);
                    //var Picture = JsonConvert.DeserializeObject<FormFile>(img_string);
                    string picture = this.AddImage(pic, IUserService.ImageType.Profile);
                    user.ProfilePicture = picture;
                    db.KeyDelete(img_key);
                }

                this.Neo.Cypher
                    .Create("(u:User $prop)")
                    .WithParam("prop",user)
                    .ExecuteWithoutResultsAsync();

                db.KeyDelete(key);
                link = this.URL.LogInPage;
            }
            return link;
        }

        public string ExtractPictureName(string url)
        {
            string[] disassembled_url = url.Split("/");
            return disassembled_url[5];
        }

        public List<string> CommonListElements(string picture_path, List<string> new_hashtags)
        {
            List<string> exceptions = new List<string>();

            List<Hashtag> hashtags = this.Neo.Cypher
                .Match("(h:Hashtag)-[:HAVE]->(p:Photo {path: $photopath})")
                .WithParam("photopath", picture_path)
                .Return(h => h.CollectAs<Hashtag>())
                .ResultsAsync.Result.ToList().Single().ToList();

            foreach(Hashtag tmp_tag in hashtags)
            {
                if(new_hashtags.Contains(tmp_tag.Title))
                {
                    exceptions.Append(tmp_tag.Title);
                }
            }

            return exceptions;
        }
        //sta ako user prati hashtag koji je obrisan prilikom azuriranja slike
        public void UpdateHashtags(string picture_path, List<string> exceptions=null)
        {
            List<Hashtag> hash_list;
            //bira samo hashtagove koji nisu u listi exceptions
            if(exceptions!=null)
            {
                hash_list = this.Neo.Cypher
                .Match("(h:Hashtag)-[r:HAVE]->(p:Photo {path: $photopath})")
                .WithParam("photopath", picture_path)
                .Where((Hashtag h) => !exceptions.Contains(h.Title))
                .Delete("r")
                .Return(h => h.CollectAs<Hashtag>())
                .ResultsAsync.Result.ToList().Single().ToList();
            }
            else
            {
                hash_list = this.Neo.Cypher
                .Match("(h:Hashtag)-[r:HAVE]->(p:Photo {path: $photopath})")
                .WithParam("photopath", picture_path)
                .Delete("r")
                .Return(h => h.CollectAs<Hashtag>())
                .ResultsAsync.Result.ToList().Single().ToList();
            }
            
            foreach (Hashtag hashtag in hash_list)
            {
                //koriscenjem cypher f-je exists(), proveriti da li relacija uopste postoji i direktno obrisati hashtag sa svim njegovim granama
                //jednom recju odraditi sve u 1 naredbi (query-ju)

                //this.Neo.Cypher
                //    .Match("(h:Hashtag {title: $val})-[r:HAVE]->(p:Photo)")
                //    .WithParam("val", hashtag.Title)
                //    .Where()

                List<Photo> photos = this.Neo.Cypher
                    .Match("(h:Hashtag {title: $val})-[r:HAVE]->(p:Photo)")
                    .WithParam("val", hashtag.Title)
                    .Return(p => p.CollectAs<Photo>())
                    .ResultsAsync.Result.ToList().Single().ToList();

                if (!photos.Any())
                {
                    this.Neo.Cypher
                        .Match("(h:Hashtag {title: $val})-[r]->()")
                        .WithParam("val", hashtag.Title)
                        .Delete("r")
                        .ExecuteWithoutResultsAsync();

                    this.Neo.Cypher
                        .Match("(h:Hashtag {title: $val})")
                        .WithParam("val", hashtag.Title)
                        .Delete("h")
                        .ExecuteWithoutResultsAsync();
                }

                //menjanje profilne (property-ja) cvora, ako ne postoji slika 

            }
        }

        public User GetUser(string username)
        {
            User user = this.Neo.Cypher
                .Match("(n:User)")
                .Where((User u) => u.UserName == username)
                .Return(n => n.As<User>())
                .ResultsAsync.Result.ToList().Single();
            return user;
        }
        public Hashtag GetOrCreateHashtag(string title)
        {
            Hashtag hTag = this.Neo.Cypher
                .Match("(h:Hashtag)")
                .Where((Hashtag h) => h.Title == title)
                .Return(h => h.As<Hashtag>())
                .ResultsAsync.Result.ToList().Single();
            if (hTag != null)
            {
                return hTag;
            }
            else
            {
                this.Neo.Cypher
                    .Create("(h:Hashtag $prop)")
                    .WithParam("prop", hTag)
                    .ExecuteWithoutResultsAsync();
                return hTag;
            }
        }
        public string GenerateCookie(int length = 25)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random(); //probaj sa RandomNumberGenerator klasom
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        public void StoreCookie(string key, string mail)
        {
            var db = this.Redis.GetDatabase();
            db.StringSetAsync(key, mail);
        }
        public string? CheckCookie(string key)
        {
            var db = this.Redis.GetDatabase();
            if(db.KeyExists(key))
            {
                return db.StringGetAsync(key).Result;
            }
            else
            {
                return null;
            }
        }

        public void DeleteCookie(string key)
        {
            var db = this.Redis.GetDatabase();
            if(db.KeyExists(key))
            {
                //obrisati key
                db.KeyDeleteAsync(key);
            }
        }

        public bool IsFromLast24h(DateTime timeForChecking)
        {
            DateTime now = DateTime.Now;
            if (timeForChecking > now.AddHours(-24) && timeForChecking <= now)
                return true;
            return false;
        }

    }
}
