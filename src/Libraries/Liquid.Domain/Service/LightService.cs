using Liquid.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Liquid.Domain
{
    /// <summary>
    /// Basic class to implement business domain logic in Object/Component orientation style
    /// </summary>
    public abstract class LightService : LightDomain
    {
        /// <summary>
        /// List of lightDomain delegates factoried
        /// </summary>
        protected readonly Dictionary<Type, object> delegates = [];

        /// <summary>
        /// Returns an instance of a domain LightService 
        /// responsible for delegate (business) functionality
        /// </summary>
        /// <typeparam name="T">the delegate LightDomain class</typeparam>
        /// <returns>Instance of the LightDomain class</returns>
        protected virtual T Service<T>() where T : LightService, new()
        {
            T domain = (T)delegates.FirstOrDefault(s => s.Key == typeof(T)).Value;

            if (domain is null)
            {
                domain = FactoryDomain<T>();
                delegates.Add(typeof(T), domain);
            }
            return domain;
        }

        internal override void ExternalInheritanceNotAllowed() { }
    }
}