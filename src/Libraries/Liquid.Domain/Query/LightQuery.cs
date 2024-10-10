using Liquid.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Liquid.Domain
{
    /// <summary>
    /// A CQRS query prototype (ancestor)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LightQuery<T> : LightDomain
    {
        /// <summary>
        /// List of lightDomain delegates factoried
        /// </summary>
        protected readonly Dictionary<Type, object> delegates = [];

        /// <summary>
        /// The parameters for the query
        /// </summary>
        protected T Query { get; set; }

        /// <summary>
        /// Runs the query
        /// </summary>
        /// <param name="query">Query parameters</param>
        /// <returns>Domain response</returns>
        public async Task<DomainResponse> RunAsync(T query)
        {
            //Injects the command and call business domain logic to handle it
            Query = query;

            Telemetry.TrackEvent($"Query {this.GetType().Name}", $"userId: {CurrentUserId}");

            //Calls execute operation asyncronously
            return await ExecuteAsync();
        }

        /// <summary>
        /// Method to implement the actual execution of the query
        /// </summary>
        /// <returns></returns>
        protected abstract Task<DomainResponse> ExecuteAsync();
        /// <summary>
        /// Returns an instance of a domain LightService 
        /// responsible for delegate (business) functionality
        /// </summary>
        /// <typeparam name="U">the delegate LightDomain class</typeparam>
        /// <returns>Instance of the LightDomain class</returns>
        protected virtual U Service<U>() where U : LightDomain, new()
        {
            U domain = (U)delegates.FirstOrDefault(s => s.Key == typeof(U)).Value;

            if (domain is null)
            {
                domain = FactoryDomain<U>();
                delegates.Add(typeof(U), domain);
            }
            return domain;
        }

        internal override void ExternalInheritanceNotAllowed() { }
    }

    /// <summary>
    /// A generic query without any parameters
    /// </summary>
    public abstract class LightQuery : LightQuery<EmptyQueryRequest>
    {

        /// <summary>
        /// Runs the query without any parameters
        /// </summary>
        /// <returns>Domain response</returns>
        public async Task<DomainResponse> RunAsync()
        {
            Query = default;

            Telemetry.TrackEvent($"Query {this.GetType().Name.Replace("Query", "")}");

            //Calls execute operation asyncronously
            return await ExecuteAsync();
        }

        internal override void ExternalInheritanceNotAllowed() { }
    }
}