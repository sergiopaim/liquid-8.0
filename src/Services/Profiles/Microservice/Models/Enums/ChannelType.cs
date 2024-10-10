using Liquid.Domain;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class ChannelType(string code) : LightLocalizedEnum<ChannelType>(code)
    {
        public static readonly ChannelType Email = new(nameof(Email));
        public static readonly ChannelType Phone = new(nameof(Phone));
        public static readonly ChannelType App = new(nameof(App));
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}