namespace Liquid.Domain
{
    /// <summary>
    /// Basic class to implement business domain (query) logic in a CQRS style
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LightQueryRequest<T> : LightViewModel<T> where T : LightQueryRequest<T>, new() { }

    /// <summary>
    /// Id for the query operation
    /// </summary>
    public class ByIdQueryRequest : LightCommandRequest<ByIdQueryRequest>
    {
        /// <summary>
        /// The id of entity
        /// </summary>
        public string Id { get; set; }

        public override void ValidateModel() { }
    }

    /// <summary>
    /// Empty request for queries without request parameters
    /// </summary>
    public class EmptyQueryRequest : LightCommandRequest<EmptyQueryRequest>
    {
        public override void ValidateModel() { }
    }
}