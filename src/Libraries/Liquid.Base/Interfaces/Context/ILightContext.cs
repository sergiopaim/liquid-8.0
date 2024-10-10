using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Liquid.Base
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Global Context interface for Microservice
    /// </summary>
    public interface ILightContext
    {
        ClaimsPrincipal User { get; set; }
        string OperationId { get; set; }
        public IHttpContextAccessor HttpContextAccessor { get; }
        string CurrentUserId { get; }
        string CurrentUserFirstName { get; }
        string CurrentUserFullName { get; }
        string CurrentUserEmail { get; }
        bool CurrentUserIsInRole(string role);
        bool CurrentUserIsInAnyRole(string roles);
        bool CurrentUserIsInAnyRole(params string[] roles);
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}