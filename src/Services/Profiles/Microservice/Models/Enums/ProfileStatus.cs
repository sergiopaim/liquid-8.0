using Liquid.Domain;

namespace Microservice.ViewModels
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class ProfileStatus(string code) : LightEnum<ProfileStatus>(code)
    {
        public static readonly ProfileStatus Invited = new(nameof(Invited));
        public static readonly ProfileStatus Active = new(nameof(Active));
        public static readonly ProfileStatus Inactive = new(nameof(Inactive));
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

}