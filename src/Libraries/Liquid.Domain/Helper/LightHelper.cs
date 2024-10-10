using Liquid.Base;
using Liquid.Interfaces;

namespace Liquid.Domain
{
    public class LightHelper
    {
        #region Context properties and methods

        /// <summary>
        /// The critic handler
        /// </summary>
        static public ICriticHandler CriticHandler => WorkBench.CriticHandler;

        /// <summary>
        /// The current active telemetry service
        /// </summary>
        static public ILightTelemetry Telemetry => WorkBench.Telemetry;

        /// <summary>
        /// Current session context
        /// </summary>
        static public ILightContext SessionContext => WorkBench.SessionContext;

        /// <summary>
        /// Gets the id of the current user
        /// </summary>
        static public string CurrentUserId => SessionContext.CurrentUserId;

        /// <summary>
        /// Gets the first name of the current user
        /// </summary>
        static public string CurrentUserFirstName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the full name of the current user
        /// </summary>
        static public string CurrentUserFullName => SessionContext.CurrentUserFirstName;

        /// <summary>
        /// Gets the e-mail address of the current user
        /// </summary>
        static public string CurrentUserEmail => SessionContext.CurrentUserEmail;

        /// <summary>
        /// Checks if the current user is in the given security role
        /// </summary>
        /// <param name="role">Security role</param>
        /// <returns>True if the user is in the role</returns>
        static public bool CurrentUserIsInRole(string role) => SessionContext.CurrentUserIsInRole(role);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">Security roles in a comma separated string</param>
        /// <returns>True if the user is in any role</returns>
        static public bool CurrentUserIsInAnyRole(string roles) => SessionContext.CurrentUserIsInAnyRole(roles);

        /// <summary>
        /// Checks if the current user is in any of the given security roles
        /// </summary>
        /// <param name="roles">List of security roles</param>
        /// <returns>True if the user is in any role</returns>
        static public bool CurrentUserIsInAnyRole(params string[] roles) => SessionContext.CurrentUserIsInAnyRole(roles);

        /// <summary>
        /// Indicates whether at least one Business error has been issued
        /// </summary>
        static public bool HasBusinessErrors => CriticHandler.HasBusinessErrors;

        #region AddBusinessError

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        static public void AddBusinessError(string errorCode)
        {
            CriticHandler.AddBusinessError(errorCode);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">error code</param>
        /// <param name="message">error message</param>
        static public void AddBusinessError(string errorCode, string message)
        {
            CriticHandler.AddBusinessError(errorCode, [message]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on errorCode</param>
        static public void AddBusinessError(string errorCode, params object[] args)
        {
            CriticHandler.AddBusinessError(errorCode, args);
        }

        #endregion

        #region AddBusinessWarning

        /// <summary>
        /// Method add the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        static public void AddBusinessWarning(string warningCode)
        {
            CriticHandler.AddBusinessWarning(warningCode);
        }

        /// <summary>
        /// Method add the warning to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        static public void AddBusinessWarning(string warningCode, string message)
        {
            CriticHandler.AddBusinessWarning(warningCode, [message]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on warningCode</param>
        static public void AddBusinessWarning(string warningCode, params object[] args)
        {
            CriticHandler.AddBusinessWarning(warningCode, args);
        }

        #endregion

        #region AddBusinessInfo

        /// <summary>
        /// /// Method add the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        static public void AddBusinessInfo(string infoCode)
        {
            CriticHandler.AddBusinessInfo(infoCode);
        }

        /// <summary>
        /// /// Method add the information to the Critic Handler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="message">error message</param>
        static public void AddBusinessInfo(string infoCode, string message)
        {
            CriticHandler.AddBusinessInfo(infoCode, [message]);
        }

        /// <summary>
        /// Method add the error code to the CriticHandler
        /// and add in Critics list to build the object InvalidInputLightException
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on infoCode</param>
        static public void AddBusinessInfo(string infoCode, params object[] args)
        {
            CriticHandler.AddBusinessInfo(infoCode, args);
        }

        #endregion

        #endregion
    }
}