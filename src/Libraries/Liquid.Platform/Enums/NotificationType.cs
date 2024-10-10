using Liquid.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Liquid.Platform
{
    /// <summary>
    /// Type of notifications sent to users
    /// </summary>
    public class NotificationType(string code) : LightLocalizedEnum<NotificationType>(code)
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public int Order => GetOrder(Code);
        public bool SendOnlyIfChannelIsValid => IsToSendOnlyIfChannelIsValid(Code);
        public bool SendEvenIfChannelIsNotValid => IsToSendEvenIfChannelIsNotValid(Code);

        public static readonly NotificationType Direct = new(nameof(Direct));
        public static readonly NotificationType Account = new(nameof(Account));
        public static readonly NotificationType Tasks = new(nameof(Tasks));
        public static readonly NotificationType Marketing = new(nameof(Marketing));

        public static List<NotificationType> GetSendOnlyIfChannelIsValid() => [Account, Tasks, Marketing];
        public static List<NotificationType> GetSendEvenIfChannelIsNotValid() => [Direct];

        public static bool IsToSendOnlyIfChannelIsValid(string code) => GetSendOnlyIfChannelIsValid().Any(s => s.Code == code);
        public static bool IsToSendEvenIfChannelIsNotValid(string code) => GetSendEvenIfChannelIsNotValid().Any(s => s.Code == code);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}