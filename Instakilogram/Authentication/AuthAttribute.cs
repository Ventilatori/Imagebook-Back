using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Instakilogram.Authentication
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthAttribute : Attribute, IAuthorizationFilter
    {       
        public AuthAttribute()
        {
        }
        
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string mail = (string)context.HttpContext.Items["User"];
            if (mail == null)
            {
                context.Result = new JsonResult(new { message = "Neovlascen" }) { StatusCode = StatusCodes.Status401Unauthorized };
            }
        }
    }
}
