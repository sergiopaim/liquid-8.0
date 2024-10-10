using LinqKit;

namespace Liquid.Domain
{
    /// <summary>
    /// A linq policy specification
    /// </summary>
    /// <typeparam name="T">The type to apply the specification</typeparam>
    public interface ILightSpecification<T>
    {
        /// <summary>
        /// Returns the specification as a Linq where clause
        /// </summary>
        /// <returns>The linq where clause</returns>
        ExpressionStarter<T> WhereClause { get; }
        /// <summary>
        /// Checks if the instance matches (satisfies) the policy specification
        /// </summary>
        /// <param name="instance">The instance to evaluate the match</param>
        /// <returns>True if matches</returns>
        bool Matches(T instance);
        /// <summary>
        /// Compose the current specification and the one as parameter with an AND operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        ILightSpecification<T> And(ILightSpecification<T> specification);
        /// <summary>
        /// Compose the current specification and the one as parameter with an OR operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        ILightSpecification<T> Or(ILightSpecification<T> specification);
        /// <summary>
        /// Returns the current specification with a NOT operator
        /// </summary>
        /// <returns>The composed specification</returns>
        ILightSpecification<T> Not();
        /// <summary>
        /// Compose the current specification and negating the one as parameter with an NOT operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        ILightSpecification<T> Not(ILightSpecification<T> specification);
    }
}