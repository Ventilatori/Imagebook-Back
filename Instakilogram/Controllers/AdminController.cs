using Imagebook.Authentication;
using Imagebook.Service;
using Microsoft.AspNetCore.Mvc;
using Neo4jClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imagebook.Controllers
{
    [Auth("Admin")]
    [ApiController]
    [Route("[controller]")]
    public class AdminController : Controller
    {
        private IGraphClient Neo;
        private IUserService Service;

        public AdminController(IGraphClient gc, IUserService service)
        {
            this.Neo = gc;
            Service = service;
        }

    }
}
