using Liquid.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Liquid.Domain
{
    /// <summary>
    /// A CQRS command prototype (ancestor)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class LightCommand<T> : LightDomain
    {
        /// <summary>
        /// List of lightDomain delegates factoried
        /// </summary>
        protected readonly Dictionary<Type, object> delegates = [];

        /// <summary>
        /// The parameters for the command
        /// </summary>
        protected T Command { get; set; }

        /// <summary>
        /// Runs the command
        /// </summary>
        /// <param name="command">Command parameters</param>
        /// <returns>Domain response</returns>
        public async Task<DomainResponse> RunAsync(T command)
        {
            //Injects the command and call business domain logic to handle it
            Command = command;

            Telemetry.TrackEvent($"Command {this.GetType().Name}", $"userId: {CurrentUserId}");

            //Calls execute operation asyncronously
            return await ExecuteAsync();
        }

        /// <summary>
        /// Method to implement the actual execution of the command
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

        /// <summary>
        /// Returns an instance of a (sub) LightCommand
        /// responsible for delegate (business) functionality
        /// </summary>
        /// <typeparam name="U">the delegate LightDomain class</typeparam>
        /// <returns>Instance of the LightDomain class</returns>
        protected virtual U SubCommand<U>() where U : LightDomain, new()
        {
            return Service<U>();
        }

        internal override void ExternalInheritanceNotAllowed() { }
    }

    /// <summary>
    /// A generic command without any parameters
    /// </summary>
    public abstract class LightCommand : LightCommand<EmptyCommandRequest>
    {
        /// <summary>
        /// Runs the command without any parameters
        /// </summary>
        /// <returns>Domain response</returns>
        public async Task<DomainResponse> RunAsync()
        {
            Command = default;

            Telemetry.TrackEvent($"Command {this.GetType().Name.Replace("Command", "")}");

            //Calls execute operation asyncronously
            return await ExecuteAsync();
        }

        internal override void ExternalInheritanceNotAllowed() { }
    }
}