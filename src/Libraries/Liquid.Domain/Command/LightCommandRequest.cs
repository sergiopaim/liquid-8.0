namespace Liquid.Domain
{
    /// <summary>
    /// Basic class to implement business domain (command) logic in a CQRS style
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LightCommandRequest<T> : LightViewModel<T> where T : LightCommandRequest<T>, new() { }

    /// <summary>
    /// Id for the command operation
    /// </summary>
    public class ByIdCommandRequest : LightCommandRequest<ByIdCommandRequest>
    {
        /// <summary>
        /// The id of entity
        /// </summary>
        public string Id { get; set; }

        public override void ValidateModel() { }
    }

    /// <summary>
    /// Empty request for commands without request parameters
    /// </summary>
    public class EmptyCommandRequest : LightCommandRequest<EmptyCommandRequest>
    {
        public override void ValidateModel() { }
    }
}