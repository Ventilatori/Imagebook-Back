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

namespace Instakilogram.Service
{
    public interface IUserService
    {
        enum MailType
        {
            Verifikacija,
            ResetPassword
        };
        //enum ImageType
        //{
        //    Prodavac,
        //    Proizvod
        //};
        //string DodajSliku(IFormFile? slika, ImageType tipSlike, string tipProizvoda = null);
        //bool ObrisiSliku(string slika, ImageType tipSlike, string tipProizvoda = null);
        int PinGenerator();
        //void PinUpdate(Korisnik korisnik, int PIN);
        bool ProveriSifru(byte[] sifra, byte[] salt, string zahtev);
        void HesirajSifru(out byte[] hash, out byte[] salt, string sifra);
        //void PosaljiMail(Korisnik korisnik, MailType tip);

    }

    public class UserService : IUserService
    {
        public MailSettings _mailSettings { get; set; }
        public IWebHostEnvironment Environment { get; set; }
        public IGraphClient Neo;
        public URLs URL { get; set; }

        public UserService(IGraphClient gc, IOptions<MailSettings> mailSettings, IOptions<URLs> url, IWebHostEnvironment environment) {
            this.Neo = gc;
            this._mailSettings = mailSettings.Value;
            this.URL = url.Value;
            this.Environment = environment;
        }
        //public string DodajSliku(IFormFile? slika, IUserService.ImageType tipSlike, string tipProizvoda = null)
        //{
        //    string folderPath = "Slike\\";
        //    switch (tipSlike)
        //    {
        //        case IUserService.ImageType.Prodavac:
        //            folderPath += tipSlike.ToString();
        //            break;
        //        case IUserService.ImageType.Proizvod:
        //            folderPath += tipSlike.ToString() + "\\" + tipProizvoda;
        //            break;
        //        default:
        //            return null;
        //    }
        //    string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
        //    string imeFajla;
        //    if (slika != null)
        //    {
        //        imeFajla = Guid.NewGuid().ToString() + "_" + slika.FileName;
        //        string filePath = Path.Combine(uploadsFolder, imeFajla);
        //        slika.CopyTo(new FileStream(filePath, FileMode.Create));
        //    }
        //    else
        //    {
        //        imeFajla = "default.png";
        //    }
        //    return imeFajla;
        //}
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
        public bool ProveriSifru(byte[] sifra, byte[] salt, string zahtev)
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
        public void HesirajSifru(out byte[] hash, out byte[] salt, string sifra)
        {
            HMACSHA512 hashObj = new HMACSHA512();
            salt = hashObj.Key;
            byte[] password = Encoding.UTF8.GetBytes(sifra);
            hash = hashObj.ComputeHash(password);
        }
        //public void PosaljiMail(Korisnik korisnik, IUserService.MailType tip)
        //{
        //    MailMessage msg = new MailMessage();
        //    msg.From = new MailAddress(_mailSettings.Adresa, _mailSettings.Ime);
        //    msg.To.Add(korisnik.Mail);
        //    msg.Subject = _mailSettings.Subject;

        //    if (tip == IUserService.MailType.Verifikacija)
        //    {
        //        string path = Path.Combine(Environment.WebRootPath, "Mail\\Verify.html");
        //        string text = System.IO.File.ReadAllText(path);
        //        text = text.Replace("~", URL.VerifikacijaURL + korisnik.ID);
        //        msg.Body = text;
        //    }
        //    else if (tip == IUserService.MailType.ResetPassword)
        //    {
        //        string path = Path.Combine(Environment.WebRootPath, "Mail\\PasswordReset.html");
        //        string text = System.IO.File.ReadAllText(path);
        //        int PIN = PinGenerator();
        //        this.PinUpdate(korisnik, PIN);
        //        text = text.Replace("`", PIN.ToString());
        //        text = text.Replace("~", URL.PasswordResetURL + korisnik.ID);
        //        msg.Body = text;
        //    }

        //    msg.IsBodyHtml = true;

        //    var smtpClient = new SmtpClient("smtp.gmail.com");
        //    smtpClient.UseDefaultCredentials = false;
        //    smtpClient.Credentials = new NetworkCredential(_mailSettings.Adresa, _mailSettings.Password);
        //    smtpClient.Port = 587;
        //    smtpClient.EnableSsl = true;
        //    smtpClient.Send(msg);

        //}
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
        
        
    }
}
