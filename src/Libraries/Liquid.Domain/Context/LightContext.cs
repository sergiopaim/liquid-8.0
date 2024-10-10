using Liquid.Base;
using Liquid.Runtime;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Security.Claims;

namespace Liquid.Domain
{
    /// <summary>
    /// Global context for microservice
    /// </summary>
    public class LightContext : ILightContext
    {
        /// <summary>
        /// User with Claims
        /// </summary>
        public ClaimsPrincipal User { get; set; }
        public string OperationId { get; set; }
        public IHttpContextAccessor HttpContextAccessor { get; set; }

        public string CurrentUserId => User?.FindFirstValue("sub") ?? User?.FindFirstValue(JwtClaimTypes.UserId);

        public string CurrentUserFirstName => User?.FindFirstValue("GivenName") ?? "";

        public string CurrentUserFullName => CurrentUserFirstName + " " + User?.FindFirstValue("Surname") ?? "";

        public string CurrentUserEmail => User?.FindFirstValue("Email") ?? "";

        /// <summary>
        /// Constructor
        /// </summary>
        public LightContext() { }
        /// <summary>
        /// Constructor with HttpContext Accessor
        /// </summary>
        /// <param name="httpContextAccessor"></param>
        public LightContext(IHttpContextAccessor httpContextAccessor)
        {
            HttpContextAccessor = httpContextAccessor;
        }

        public bool CurrentUserIsInRole(string role) => User?.IsInRole(role) ?? false;

        public bool CurrentUserIsInAnyRole(string roles)
        {
            if (User is null)
                return false;

            return roles.Split(",")
                        .Any(r => User.IsInRole(r.Trim()));
        }

        public bool CurrentUserIsInAnyRole(params string[] roles)
        {
            if (User is null)
                return false;

            return roles.Any(r => User.IsInRole(r.Trim()));
        }
    }
}