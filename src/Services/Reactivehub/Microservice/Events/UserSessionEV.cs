using Liquid.Activation;

namespace Microservice.Events
{
    /// <summary>
    /// Reactive Event indicating that a notification was sent to the user
    /// </summary>
    public class UserSessionEV : LightReactiveEvent<UserSessionEV>
    {

        /// <summary>
        /// User's session id
        /// </summary>
        public string SessionId { get; set; }
        /// <summary>
        /// Type of UI Event
        /// </summary>
        public string UIEvent { get; set; }
        /// <summary>
        /// Event context
        /// </summary>
        public string Context { get; set; }
        /// <summary>
        /// Short message to be promptly shown to user
        /// </summary>

        public override void ValidateModel() { }
    }
}