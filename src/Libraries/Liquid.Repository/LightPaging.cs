using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Liquid.Repository
{
    /// <summary>
    /// Structure for paginating database queries
    /// </summary>
    /// <typeparam name="T">Type of the data being paginated</typeparam>
    public class LightPaging<T> : ILightPaging<T>
    {
        /// <summary>
        /// Entity data
        /// </summary>
        public ICollection<T> Data { get; set; }
        /// <summary>
        /// Number of registers per page
        /// </summary>
        public int ItemsPerPage { get; set; }
        /// <summary>
        /// Database index of the current page to get the next one
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Creates a new LightPaging of LightViewModel from a LightPaging
        /// </summary>
        /// <typeparam name="U">A LightViewModel type</typeparam>
        /// <param name="origin">The origin LightPaging</param>
        /// <param name="conversion">The function to convert ILightModel instances to T instances <para>NOTE: if null is returned, no T instance from ILightModel instance will be inserted in the returned LightPaging.Data</para></param>
        /// <returns>The new LightPaging</returns>
        public static LightPaging<T> FactoryFrom<U>(ILightPaging<U> origin, Func<U, T> conversion) where U : ILightModel
        {
            if (origin is null)
                throw new LightException("origin parameter cannot be null");
            if (conversion is null)
                throw new LightException("lambda expression parameter cannot be null");

            var newPaging = new LightPaging<T>
            {
                Data = [],
                ItemsPerPage = origin.ItemsPerPage,
                ContinuationToken = origin.ContinuationToken
            };

            foreach (var item in origin.Data)
            {
                var converted = conversion.Invoke(item);

                if (converted is not null)
                    newPaging.Data.Add(converted);
            }

            return newPaging;
        }

        /// <summary>
        /// Creates a new LightPaging of JsonDocument from a LightPaging
        /// </summary>
        /// <param name="origin">The origin LightPaging</param>
        /// <param name="conversion">The optional function to convert JsonDocument instances to T instances <para>If not informed, standard JsonDocument.ToObject will be used.</para><para>NOTE: if null is returned, no T instance from ILightModel instance will be inserted in the returned LightPaging.Data</para></param>
        /// <returns>The new LightPaging</returns>
        public static LightPaging<T> FactoryFrom(ILightPaging<JsonDocument> origin, Func<JsonDocument, T> conversion = null)
        {
            if (origin is null)
                throw new LightException("origin parameter cannot be null");

            var newPaging = new LightPaging<T>
            {
                Data = [],
                ItemsPerPage = origin.ItemsPerPage,
                ContinuationToken = origin.ContinuationToken
            };

            if (conversion is null)
                foreach (var item in origin.Data)
                {
                    var converted = item.ToObject<T>();

                    if (converted is not null)
                        newPaging.Data.Add(converted);
                }
            else
                foreach (var item in origin.Data)
                {
                    var converted = conversion.Invoke(item);

                    if (converted is not null)
                        newPaging.Data.Add(converted);
                }

            return newPaging;
        }
    }
}