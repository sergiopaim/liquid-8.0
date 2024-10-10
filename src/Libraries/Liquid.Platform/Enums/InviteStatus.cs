using Liquid.Domain;

namespace Liquid.Platform
{
    /// <summary>
    /// Type of notifications sent to users
    /// </summary>
    public class InviteStatus(string code) : LightLocalizedEnum<InviteStatus>(code)
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static readonly InviteStatus Accepted = new(nameof(Accepted));
        public static readonly InviteStatus Pending = new(nameof(Pending));
        public static readonly InviteStatus Bounced = new(nameof(Bounced));
        public static readonly InviteStatus Reinvite = new(nameof(Reinvite));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
} 