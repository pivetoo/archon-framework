using Archon.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Archon.Api.Security
{
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        public long? UserId
        {
            get
            {
                string? value = httpContextAccessor.HttpContext?.User.FindFirst("user_id")?.Value;
                return long.TryParse(value, out long userId) ? userId : null;
            }
        }

        public string? UserName => httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;

        public string? Email => httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;

        public string? ClientId => httpContextAccessor.HttpContext?.User.FindFirst("client_id")?.Value;
    }
}
