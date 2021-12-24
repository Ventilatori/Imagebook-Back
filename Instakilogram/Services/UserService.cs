using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Neo4jClient;
using Instakilogram.Models;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace Instakilogram.Service
{
    public interface IUserService
    {
        enum MailType
        {
            Verify,
            ResetPassword
        };
        enum ImageType
        {
            Standard,
            Profile
        };
        string AddImage(IFormFile? picture, IUserService.ImageType img_type = IUserService.ImageType.Standard);
        //bool ObrisiSliku(string slika, ImageType tipSlike, string tipProizvoda = null);
        int PinGenerator();
        //void PinUpdate(Korisnik korisnik, int PIN);
        bool CheckPassword(byte[] sifra, byte[] salt, string zahtev);
        void PasswordHash(out string hash_string, out string salt_string, string sifra);
        void SendMail(User user, MailType type);
        bool UserExists(string new_user_name, string new_mail);
        void TmpStoreAccount(User user, IFormFile Picture = null);
        string ApproveAccount(string key);
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
        public string AddImage(IFormFile? picture, IUserService.ImageType img_type = IUserService.ImageType.Standard)
        {
            string folderPath = "Images\\"+img_type.ToString();
            string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
            string file_path;
            if (picture != null)
            {
                file_path = Guid.NewGuid().ToString() + "_" + picture.FileName;
                string filePath = Path.Combine(uploadsFolder, file_path);
                picture.CopyTo(new FileStream(file_path, FileMode.Create));
            }
            else
            {
                file_path = "default.png";
            }
            return file_path;
        }
        //public bool ObrisiSliku(string slika, IUserService.ImageType tipSlike, string tipProizvoda = null)
        //{
        //    if(!String.Equals(slika, "default.png"))
        //    {
        //        string folderPath = "Slike\\";
        //        switch (tipSlike)
        //        {
        //            case IUserService.ImageType.Prodavac:
        //                folderPath += tipSlike.ToString();
        //                break;
        //            case IUserService.ImageType.Proizvod:
        //                folderPath += tipSlike.ToString() + "\\" + tipProizvoda;
        //                break;
        //            default:
        //                return false;
        //        }
        //        string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
        //        string filePath = Path.Combine(uploadsFolder, slika);

        //        if (System.IO.File.Exists(filePath))
        //        {
        //            System.IO.File.Delete(filePath);
        //            return true;
        //        }
        //        else
        //        {
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        return true;
        //    }
        //}

        //mozda moze da se iskoristi za cookie

        //public string GenerisiToken(Korisnik korisnik)
        //{
        //    JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        //    byte[] key = Encoding.ASCII.GetBytes(_appSettings.Secret);
        //    SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
        //    {
        //        Subject = new ClaimsIdentity(new[] { new Claim("id", korisnik.ID.ToString()) }),
        //        Expires = DateTime.UtcNow.AddDays(1),
        //        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        //    };
        //    SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        //    return tokenHandler.WriteToken(token);
        //}
        public bool CheckPassword(byte[] sifra, byte[] salt, string zahtev)
        {
            HMACSHA512 hashObj = new HMACSHA512(salt);
            byte[] password = System.Text.Encoding.UTF8.GetBytes(zahtev);
            byte[] hash = hashObj.ComputeHash(password);

            int len = hash.Length;
            for (int i = 0; i < len; i++)
            {
                if (sifra[i] != hash[i])
                {
                    return false;
                }
            }
            return true;
        }
        public void PasswordHash(out string hash_string, out string salt_string, string password_string)
        {
            byte[] hash, salt;
            HMACSHA512 hashObj = new HMACSHA512();
            salt = hashObj.Key;
            byte[] password = Encoding.UTF8.GetBytes(password_string);
            hash = hashObj.ComputeHash(password);
            hash_string = Encoding.UTF8.GetString(hash);
            salt_string = Encoding.UTF8.GetString(salt);
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
            //else if (type == IUserService.MailType.ResetPassword)
            //{
            //    string path = Path.Combine(Environment.WebRootPath, "Mail\\ResetPassword.html");
            //    string text = System.IO.File.ReadAllText(path);
            //    int PIN = PinGenerator();
            //    this.PinUpdate(korisnik, PIN);
            //    text = text.Replace("`", PIN.ToString());
            //    text = text.Replace("~", URL.PasswordResetURL + korisnik.ID);
            //    msg.Body = text;
            //}

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
        //public void PinUpdate(Korisnik korisnik, int PIN) {
        //    korisnik.PIN = PIN;
        //    Context.Update<Korisnik>(korisnik);
        //    Context.SaveChanges();
        //}

        public bool UserExists(string new_user_name, string new_mail)
        {
            var query = this.Neo.Cypher
                .Match("(n:User)")
                .Where((User u) => u.UserName == new_user_name || u.Mail == new_mail)
                .Return(n => n.As<User>())
                .ResultsAsync;
            User result = query.Result.ToList().Single();
            if(result != null)
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
                string img_string = JsonConvert.SerializeObject(Picture);
                db.StringSetAsync(user.UserName + "Profile", img_string, t);
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
                    IFormFile Picture = JsonConvert.DeserializeObject<FormFile>(img_string);
                    string picture = this.AddImage(Picture, IUserService.ImageType.Profile);
                    user.ProfilePicture = picture;
                    db.KeyDelete(img_key);
                }

                this.Neo.Cypher
                    .Create("(n:User $prop)")
                    .WithParam("prop",user)
                    .ExecuteWithoutResultsAsync();

                db.KeyDelete(key);
                link = this.URL.VerifyURL;
            }
            return link;
        }
    }
}
