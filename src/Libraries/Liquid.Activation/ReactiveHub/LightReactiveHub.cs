using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class LightReactiveHub : Hub, ILightReactiveHub
    {
        private readonly InputValidator inputValidator = new();

#pragma warning disable CA1822 // Mark members as static
        protected ICriticHandler CriticHandler => WorkBench.CriticHandler;
        protected ILightTelemetry Telemetry => WorkBench.Telemetry;
        protected HubCallerContext CallerContext => Context;

        /// <summary>
        /// Gets the id of the current user
        /// </summary>
        protected string CurrentUserId => WorkBench.SessionContext.CurrentUserId;

        /// <summary>
        /// Gets the first name of the current user
        /// </summary>
        protected string CurrentUserFirstName => WorkBench.SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the full name of the current user
        /// </summary>
        protected string CurrentUserFullName => WorkBench.SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the e-mail address of the current user
        /// </summary>
        protected string CurrentUserEmail => WorkBench.SessionContext.CurrentUserEmail;

        /// <summary>
        /// Checks if the current user is in the given security role
        /// </summary>
        /// <param name="role">Security role</param>
        /// <returns>True if the user is in the role</returns>
        protected bool CurrentUserIsInRole(string role) => WorkBench.SessionContext.CurrentUserIsInRole(role);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">Security roles in a comma separated string</param>
        /// <returns>True if the user is in any role</returns>
        protected bool CurrentUserIsInAnyRole(string roles) => WorkBench.SessionContext.CurrentUserIsInAnyRole(roles);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">List of security roles</param>
        /// <returns>True if the user is in any role</returns>
        protected bool CurrentUserIsInAnyRole(params string[] roles) => WorkBench.SessionContext.CurrentUserIsInAnyRole(roles);

        private static LightHubConnection connection;
        protected static LightHubConnection Connection { get => connection; set => connection = value; }

        private LightContext GetContext()
        {
            var httpContext = Context.GetHttpContext();

            string operationId = null;
            if (httpContext.Request.Headers.TryGetValue("traceparent", out StringValues headerValue))
            {
                string traceContext = headerValue;
                var splits = traceContext.Split("-");
                if (splits.Length > 1)
                    operationId = splits[1];
            }
            else if (httpContext.Request.Headers.TryGetValue("Operation-Id", out headerValue))
                operationId = headerValue;

            operationId ??= WorkBench.GenerateNewOperationId();

            return new()
            {
                User = Context.User,
                OperationId = operationId
            };
        }

        public string GetHubEndpoint()
        {
            var reactiveHubAttribute = GetType().CustomAttributes.FirstOrDefault(attr => attr.AttributeType.Equals(typeof(ReactiveHubAttribute)));
            if (reactiveHubAttribute is not null)
            {
                var hubEndpointPosition = reactiveHubAttribute.Constructor.GetParameters().FirstOrDefault(arg => arg.Name == "hubEndpoint")?.Position;
                return hubEndpointPosition.HasValue ?
                    reactiveHubAttribute.ConstructorArguments[hubEndpointPosition.Value].Value.ToString()
                    : null;
            }
            return null;
        }

        /// <summary>
        /// Method to build domain class
        /// </summary>
        /// <typeparam name="T">Generic Type</typeparam>
        /// <returns></returns>
        protected T Factory<T>() where T : LightDomain, new()
        {
            // Throws errors as a specific exception 
            if (inputValidator.ErrorsCount > 0)
                throw new InvalidInputLightException(inputValidator.Errors);

            if (WorkBench.SessionContext is null || WorkBench.CriticHandler is null)
                WorkBench.SetSession(GetContext(), new CriticHandler());

            return LightDomain.FactoryDomain<T>();
        }

        /// <summary>
        /// Adds an input error message
        /// </summary>
        /// <param name="message">Error message</param>
        protected void AddInputError(string message) => inputValidator.AddInputError(message);

        /// <summary>
        /// Adds an input error code 
        /// </summary>
        /// <param name="error">Error code</param>
        protected void AddInputValidationErrorCode(string error) => inputValidator.AddInputValidationErrorCode(error);

        /// <summary>
        /// Adds an input error code with interpolation values
        /// </summary>
        /// <param name="error">Error code</param>
        /// <param name="args">Values to interpolate</param>
        protected void AddInputValidationErrorCode(string error, params object[] args) => inputValidator.AddInputValidationErrorCode(error, args);

        /// <summary>
        /// Receives the ViewModel to input validation and adds on error list
        /// </summary>
        /// <param name="viewModel">The ViewModel to input validation</param>
        protected void ValidateInput(dynamic viewModel) => inputValidator.ValidateInput(viewModel);

        /// <inheritdoc/>
        public abstract void Initialize();

        /// <inheritdoc/>
        public override Task OnConnectedAsync() { return base.OnConnectedAsync(); }

        /// <inheritdoc/>
        public override Task OnDisconnectedAsync(Exception exception) { return base.OnDisconnectedAsync(exception); }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}