using Instakilogram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.RequestResponse
{
    public class GetUserResponse
    {
        public GetUserResponse(IEnumerable<User> user, IEnumerable<Photo> uploadedPhotos, IEnumerable<Photo> taggedOnPhotos)
        {
            User = user;
            UploadedPhotos = uploadedPhotos;
            this.taggedOnPhotos = taggedOnPhotos;
        }

        public IEnumerable<User> User { get; set; }
        public IEnumerable<Photo> UploadedPhotos { get; set; }
        public IEnumerable<Photo> taggedOnPhotos { get; set; }

    }
}
