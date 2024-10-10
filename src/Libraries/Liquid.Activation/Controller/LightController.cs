using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// This Controller and its action method handles incoming browser requests, 
    /// retrieves necessary model data and returns appropriate responses.
    /// </summary>
    public abstract class LightController : Controller
    {
        private readonly InputValidator inputValidator = new();

#pragma warning disable CA1822 // Mark members as static
        protected ICriticHandler CriticHandler => WorkBench.CriticHandler;
        protected ILightTelemetry Telemetry => WorkBench.Telemetry;
        public ILightContext SessionContext
        {
            get
            {
                if (WorkBench.SessionContext is null)
                    WorkBench.SetSession(GetContext(), new CriticHandler());

                return WorkBench.SessionContext;
            }
        }

        /// <summary>
        /// Gets the id of the current user
        /// </summary>
        protected string CurrentUserId => SessionContext.CurrentUserId;

        /// <summary>
        /// Gets the first name of the current user
        /// </summary>
        protected string CurrentUserFirstName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the full name of the current user
        /// </summary>
        protected string CurrentUserFullName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the e-mail address of the current user
        /// </summary>
        protected string CurrentUserEmail => SessionContext.CurrentUserEmail;

        /// <summary>
        /// Checks if the current user is in the given security role
        /// </summary>
        /// <param name="role">Security role</param>
        /// <returns>True if the user is in the role</returns>
        protected bool CurrentUserIsInRole(string role) => SessionContext.CurrentUserIsInRole(role);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">Security roles in a comma separated string</param>
        /// <returns>True if the user is in any role</returns>
        protected bool CurrentUserIsInAnyRole(string roles) => SessionContext.CurrentUserIsInAnyRole(roles);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">List of security roles</param>
        /// <returns>True if the user is in any role</returns>
        protected bool CurrentUserIsInAnyRole(params string[] roles) => SessionContext.CurrentUserIsInAnyRole(roles);
#pragma warning restore CA1822 // Mark members as static

        protected static ILightCache Cache => WorkBench.Cache;

        public IHttpContextAccessor HttpContextAccessor { get; set; }

        private LightContext GetContext()
        {
            string operationId = null;

            if (HttpContext.Request.Headers.TryGetValue("traceparent", out StringValues headerValue))
            {
                string traceContext = headerValue;
                var splits = traceContext.Split("-");
                if (splits.Length > 1)
                    operationId = splits[1];
            }
            else if (HttpContext.Request.Headers.TryGetValue("Operation-Id", out headerValue))
                operationId = headerValue;

            operationId ??= WorkBench.GenerateNewOperationId();

            return new(HttpContextAccessor)
            {
                User = HttpContext?.User,
                OperationId = operationId
            };
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

            if (WorkBench.SessionContext is null)
                WorkBench.SetSession(GetContext(), new CriticHandler());

            return LightDomain.FactoryDomain<T>();
        }

        /// <summary>
        /// Builds a IActionResult based on the DomainResponse
        /// </summary>
        /// <param name="response"></param>
        /// <returns>IAResponsible</returns>
        protected IActionResult Result(DomainResponse response)
        {
            if (response is null)
                return Ok();

            if (response.NotContent)
                return NoContent();

            if (response.ConflictMessage)
                return Conflict(response);

            if (response.BadRequestMessage)
                return BadRequest(response);

            if (response.GenericReturnMessage) 
                return StatusCode((int) response.StatusCode, response);

            return Ok(response);
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
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}