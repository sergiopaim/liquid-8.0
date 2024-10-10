using Liquid.Domain.API;
using Liquid.Interfaces;
using Liquid.Runtime;
using System.Dynamic;
using System.Security.Claims;

namespace Liquid.Base
{
    /// <summary>
    /// Class responsible for business logic and operations implemented as methods from the Domain Classes
    /// </summary>
    public abstract class LightDomain : ILightDomain
    {
        /// <summary>
        /// The current active repository service
        /// </summary>
        protected static ILightRepository Repository => WorkBench.Repository;

        /// <summary>
        /// The current active media storage service
        /// </summary>
        protected static ILightMediaStorage MediaStorage => WorkBench.MediaStorage;

#pragma warning disable CA1822 // Mark members as static

        /// <summary>
        /// The current active intelligence service
        /// </summary>
        protected static ILightIntelligence Intelligence => WorkBench.Intelligence;

        /// <summary>
        /// The critic handler
        /// </summary>
        public ICriticHandler CriticHandler => WorkBench.CriticHandler;

        /// <summary>
        /// The current active telemetry service
        /// </summary>
        public ILightTelemetry Telemetry => WorkBench.Telemetry;

        /// <summary>
        /// Current session context
        /// </summary>
        public ILightContext SessionContext => WorkBench.SessionContext;

#pragma warning restore CA1822 // Mark members as static

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

        /// <summary>
        /// Indicates whether at least one Business error has been issued
        /// </summary>
        protected bool HasBusinessErrors => CriticHandler.HasBusinessErrors;

        /// <summary>
        /// Indicates whether at least NoContent error has been issued
        /// </summary>
        protected bool HasNoContentError => CriticHandler.HasNoContentError;

        /// <summary>
        /// Indicates whether at least Conflict error has been issued
        /// </summary>
        protected bool HasConflictError => CriticHandler.HasConflictError;

        /// <summary>
        /// Resets the any NoContent error critic status
        /// </summary>
        protected void ResetNoContentError()
        {
            CriticHandler.ResetNoContentError();
        }

        /// <summary>
        /// Resets the any Conflict error critic status
        /// </summary>
        protected void ResetConflictError()
        {
            CriticHandler.ResetConflictError();
        }

        internal abstract void ExternalInheritanceNotAllowed();

        /// <summary>
        /// Instanciates a LightApi injecting current domain context
        /// </summary>
        /// <param name="apiName">The name of the API</param>
        /// <returns></returns>
        protected virtual LightApi FactoryLightApi(string apiName)
        {
            var token = JwtSecurityCustom.GetJwtToken(SessionContext.User?.Identity as ClaimsIdentity);

            return new(apiName, token, CriticHandler, SessionContext.OperationId);
        }

        /// <summary>
        /// Factories a LightMessage 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="commandType"></param>
        /// <returns></returns>
        protected virtual T FactoryLightMessage<T>(ILightEnum commandType) where T : ILightMessage, new()
        {
            var message = new T
            {
                TransactionContext = SessionContext,
                CommandType = commandType?.Code
            };
            return message;
        }

        #region NoContent

        /// <summary>
        /// Add to the scope that some critic has a not found type of error
        /// </summary>
        protected DomainResponse NoContent()
        {
            CriticHandler.StatusCode = StatusCode.NoContent;
            return Response();
        }

        #endregion

        #region Unauthorized

        /// <summary>
        /// Add to the scope that some critic has a unauthorized type of error
        /// </summary>
        protected DomainResponse Unauthorized()
        {
            CriticHandler.StatusCode = StatusCode.Unauthorized;
            return Response();
        }

        /// <summary>
        /// Add to the scope that some critic has a unauthorized type of error
        /// <param name="errorCode">error code</param>
        /// </summary>
        protected DomainResponse Unauthorized(string errorCode)
        {
            return Unauthorized(errorCode, default(string));
        }

        /// <summary>
        /// Add to the scope that some critic has a unauthorized type of error
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        /// </summary>
        protected DomainResponse Unauthorized(string errorCode, string message)
        {
            AddBusinessError(errorCode, message);
            return Unauthorized();
        }

        /// <summary>
        /// Add to the scope that some critic has a unauthorized type of error
        /// <param name="errorCode">error code</param>
        /// <param name="args">Arguments to interpolate</param>
        /// </summary>
        protected DomainResponse Unauthorized(string errorCode, params object[] args)
        {
            AddBusinessError(errorCode, args);
            return Unauthorized();
        }

        #endregion

        #region Forbidden

        /// <summary>
        /// Add to the scope that some critic has a forbidden type of error
        /// </summary>
        protected DomainResponse Forbidden()
        {
            CriticHandler.StatusCode = StatusCode.Forbidden;
            return Response();
        }

        /// <summary>
        /// Add to the scope that some critic has a forbidden type of error
        /// <param name="errorCode">error code</param>
        /// </summary>
        protected DomainResponse Forbidden(string errorCode)
        {
            return Forbidden(errorCode, default(string));
        }

        /// <summary>
        /// Add to the scope that some critic has a forbidden type of error
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        /// </summary>
        protected DomainResponse Forbidden(string errorCode, string message)
        {
            AddBusinessError(errorCode, message);
            return Forbidden();
        }

        /// <summary>
        /// Add to the scope that some critic has a forbidden type of error
        /// <param name="errorCode">error code</param>
        /// <param name="args">Arguments to interpolate</param>
        /// </summary>
        protected DomainResponse Forbidden(string errorCode, params object[] args)
        {
            AddBusinessError(errorCode, args);
            return Forbidden();
        }

        #endregion

        #region Conflict

        /// <summary>
        /// Add to the scope that some critic has a conflict type of error
        /// </summary>
        protected DomainResponse Conflict()
        {
            CriticHandler.StatusCode = StatusCode.Conflict;
            return Response();
        }

        /// <summary>
        /// Add to the scope that some critic has a conflict type of error
        /// <param name="errorCode">error code</param>
        /// </summary>
        protected DomainResponse Conflict(string errorCode)
        {
            return Conflict(errorCode, default(string));
        }

        /// <summary>
        /// Add to the scope that some critic has a conflict type of error
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        /// </summary>
        protected DomainResponse Conflict(string errorCode, string message)
        {
            AddBusinessError(errorCode, message);
            return Conflict();
        }

        /// <summary>
        /// Add to the scope that some critic has a conflict type of error
        /// <param name="errorCode">error code of the message</param>
        /// <param name="args">Arguments to interpolate</param>
        /// </summary>
        protected DomainResponse Conflict(string errorCode, params object[] args)
        {
            AddBusinessError(errorCode, args);
            return Conflict();
        }

        #endregion

        #region BadRequest

        /// <summary>
        /// Add to the scope that some critic has a bad request type of error
        /// </summary>
        protected DomainResponse BadRequest()
        {
            CriticHandler.StatusCode = StatusCode.BadRequest;
            return Response();
        }

        /// <summary>
        /// Add to the scope that some critic has a bad request type of error
        /// <param name="errorCode">error code</param>
        /// </summary>
        protected DomainResponse BadRequest(string errorCode)
        {
            return BadRequest(errorCode, default(string));
        }

        /// <summary>
        /// Add to the scope that some critic has a bad request type of error
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        /// </summary>
        protected DomainResponse BadRequest(string errorCode, string message)
        {
            AddBusinessError(errorCode, message);
            return BadRequest();
        }

        /// <summary>
        /// Add to the scope that some critic has a bad request type of error
        /// <param name="errorCode">error code</param>
        /// <param name="args">Arguments to interpolate</param>
        /// </summary>
        protected DomainResponse BadRequest(string errorCode, params object[] args)
        {
            AddBusinessError(errorCode, args);
            return BadRequest();
        }

        #endregion

        #region AddBusinessError and BusinessError

        /// <summary>
        /// Method to return the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        protected DomainResponse BusinessError(string errorCode)
        {
            AddBusinessError(errorCode);
            return Response();
        }

        /// <summary>
        /// Method to return the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        protected DomainResponse BusinessError(string errorCode, string message)
        {
            AddBusinessError(errorCode, [message]);
            return Response();
        }

        /// <summary>
        /// Method to return the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on errorCode</param>
        protected DomainResponse BusinessError(string errorCode, params object[] args)
        {
            AddBusinessError(errorCode, args);
            return Response();
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        public void AddBusinessError(string errorCode)
        {
            CriticHandler.AddBusinessError(errorCode);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        public void AddBusinessError(string errorCode, string message)
        {
            CriticHandler.AddBusinessError(errorCode, [message]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on errorCode</param>
        public void AddBusinessError(string errorCode, params object[] args)
        {
            CriticHandler.AddBusinessError(errorCode, args);
        }

        #endregion

        #region AddBusinessWarning and BusinessWarning

        /// <summary>
        /// Method return the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        protected DomainResponse BusinessWarning(string warningCode)
        {
            AddBusinessWarning(warningCode);
            return Response();
        }

        /// <summary>
        /// Method return the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        protected DomainResponse BusinessWarning(string warningCode, string message)
        {
            AddBusinessWarning(warningCode, [ message ]);
            return Response();
        }

        /// <summary>
        /// Method return the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on warningCode</param>
        protected DomainResponse BusinessWarning(string warningCode, params object[] args)
        {
            AddBusinessWarning(warningCode, args);
            return Response();
        }

        /// <summary>
        /// Method add the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        public void AddBusinessWarning(string warningCode)
        {
            CriticHandler.AddBusinessWarning(warningCode);
        }

        /// <summary>
        /// Method add the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        public void AddBusinessWarning(string warningCode, string message)
        {
            CriticHandler.AddBusinessWarning(warningCode, [ message ]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on warningCode</param>
        public void AddBusinessWarning(string warningCode, params object[] args)
        {
            CriticHandler.AddBusinessWarning(warningCode, args);
        }

        #endregion

        #region AddBusinessInfo and BusinessInfo

        /// <summary>
        /// /// Method to return the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        protected DomainResponse BusinessInfo(string infoCode)
        {
            AddBusinessInfo(infoCode);
            return Response();
        }

        /// <summary>
        /// /// Method to return the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        protected DomainResponse BusinessInfo(string infoCode, string message)
        {
            AddBusinessInfo(infoCode, [ message ]);
            return Response();
        }

        /// <summary>
        /// Method to return the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on infoCode</param>
        protected DomainResponse BusinessInfo(string infoCode, params object[] args)
        {
            AddBusinessInfo(infoCode, args);
            return Response();
        }

        /// <summary>
        /// /// Method add the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        public void AddBusinessInfo(string infoCode)
        {
            CriticHandler.AddBusinessInfo(infoCode);
        }

        /// <summary>
        /// /// Method add the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        public void AddBusinessInfo(string infoCode, string message)
        {
            CriticHandler.AddBusinessInfo(infoCode, [ message ]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on infoCode</param>
        public void AddBusinessInfo(string infoCode, params object[] args)
        {
            CriticHandler.AddBusinessInfo(infoCode, args);
        }

        #endregion

        /// <summary>
        /// Returns a DomainResponse class with data serialized on JSON
        /// </summary>
        /// <typeparam name="T">The desired type LightViewModel</typeparam>
        /// <returns>Instance of the specified DomainResponse</returns>
        protected DomainResponse Response<T>(T data)
        {
            TraceBusinessErrorsAndWarnings();
            return new DomainResponse(data.ToJsonDocument(), SessionContext, CriticHandler);
        }

        /// <summary>
        /// Returns a DomainResponse class with empty data serialized as JSON
        /// </summary>
        /// <returns>Instance of the specified DomainResponse</returns>
        protected DomainResponse Response()
        {
            TraceBusinessErrorsAndWarnings();
            return Response(new ExpandoObject());
        }

        private void TraceBusinessErrorsAndWarnings()
        {
            if (CriticHandler.HasBusinessErrors)
                foreach (var error in CriticHandler.GetBusinessErrors())
                    Telemetry.TrackTrace($"BizError '{error.Code}': {error.Message}");

            if (CriticHandler.HasBusinessWarnings)
                foreach (var warn in CriticHandler.GetBusinessWarnings())
                    Telemetry.TrackTrace($"BizWarn '{warn.Code}': {warn.Message}");
        }

        /// <summary>
        /// Returns a  instance of a LightDomain class for calling business domain logic
        /// </summary>
        /// <typeparam name="T">desired LightDomain subtype</typeparam>
        /// <returns>Instance of the specified LightDomain subtype</returns>
        public static T FactoryDomain<T>() where T : LightDomain, new()
        {
            ILightDomain service = new T();
            return (T)service;
        }
    }
}