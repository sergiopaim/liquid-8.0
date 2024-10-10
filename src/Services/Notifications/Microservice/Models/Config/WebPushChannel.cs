using Liquid.Platform;
using Liquid.Repository;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microservice.Models
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class WebPushChannel : LightValueObject<WebPushChannel>
    {
        public List<WebPushEndpoint> Endpoints { get; set; } = [];
        public List<string> NotificationTypes { get; set; } = [];

        [JsonIgnore]
        public bool HasAvailableEndpoints => Endpoints.Count > 0;
        public bool IsValidNotificationType(string type)
        {
            return type == NotificationType.Account.Code || NotificationTypes.Any(n => n.Equals(type, System.StringComparison.CurrentCultureIgnoreCase));
        }

        public override void ValidateModel()
        {

        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}