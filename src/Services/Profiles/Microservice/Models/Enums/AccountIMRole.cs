using Liquid.Domain;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class AccountIMRole(string code) : LightEnum<AccountIMRole>(code)
    {
        public static readonly AccountIMRole Member = new(nameof(Member));
        public static readonly AccountIMRole ServiceAccount = new(nameof(ServiceAccount));
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}