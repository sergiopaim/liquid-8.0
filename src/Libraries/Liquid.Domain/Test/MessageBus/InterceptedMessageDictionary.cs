﻿using Liquid.Base;
using Liquid.Domain.API;
using System.Collections.Generic;

namespace Liquid.Domain.Test
{
    /// <summary>
    /// Helper class to intercept message bus messages during tests
    /// </summary>
    public class InterceptedMessageDictionary
    {
        private readonly ApiWrapper api;

        /// <summary>
        /// Instanciates the message interceptor
        /// </summary>
        /// <param name="api">the API pointing to the respective microservice to be tested</param>
        public InterceptedMessageDictionary(ApiWrapper api)
        {
            this.api = api;
            _ = api.Put<DomainResponse>($"/messageBus/intercept/enable");
        }

        /// <summary>
        /// Filters the intercepted messages for the session (OperationId) by messageType
        /// </summary>
        /// <typeparam name="T">The time of the message</typeparam>
        /// <returns>The list of messages</returns>
        public List<T> OfType<T>()
        {
            if (string.IsNullOrWhiteSpace(api.OperationId))
                return [];

            var messageType = typeof(T).Name;

            return api.Put<Response<List<T>>>($"/messageBus/intercept/messages/{api.OperationId}/{messageType}").Content.Payload;
        }

        internal void Clear()
        {
            if (!string.IsNullOrWhiteSpace(api.OperationId))
                _ = api.Put<DomainResponse>($"/messageBus/intercept/messages/{api.OperationId}/clear");

            _ = api.Put<DomainResponse>($"/messageBus/intercept/disable");
        }
    }
}