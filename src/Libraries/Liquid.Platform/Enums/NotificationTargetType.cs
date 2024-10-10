using Liquid.Domain;

namespace Liquid.Platform
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Type of languages for choice of the users
    /// </summary>
    public class NotificationTargetType(string code) : LightEnum<NotificationTargetType>(code)
    {
        public static readonly NotificationTargetType Staff = new(nameof(Staff));
        public static readonly NotificationTargetType Member = new(nameof(Member));
        public static readonly NotificationTargetType Client = new(nameof(Client));
        public static readonly NotificationTargetType Prospect = new(nameof(Prospect));
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}