using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace UniversalTennis.Algorithm.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class ConsumerTokenValidatorMiddleware
    {
        private readonly RequestDelegate _next;
        private List<string> _validTokens = new List<string>
        {
            "ec06731e-aa5a-48f7-a45d-07a857eb0535"
        };

        public ConsumerTokenValidatorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            //Handle No Token
            if (string.IsNullOrEmpty(httpContext.Request.Query["token"]))
            {
                httpContext.Response.StatusCode = 400; //Bad Request                
                await httpContext.Response.WriteAsync("Token is missing");
                return;
            }
            else
            {
                var token = httpContext.Request.Query["token"];
                if (!_validTokens.Contains(token)) //check token validity
                {
                    httpContext.Response.StatusCode = 401; //Unauthorized
                    await httpContext.Response.WriteAsync("Invalid token");
                    return;
                }
            }
            await _next(httpContext);
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class ConsumerTokenValidatorMiddlewareExtensions
    {
        public static IApplicationBuilder UseConsumerTokenValidatorMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ConsumerTokenValidatorMiddleware>();
        }
    }
}
