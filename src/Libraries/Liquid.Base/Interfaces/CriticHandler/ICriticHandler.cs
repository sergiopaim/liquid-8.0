using System.Collections.Generic;

namespace Liquid.Interfaces
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Interface delegates to the handler that creates the Critics lists
    /// </summary> 
    public interface ICriticHandler
    {
        public bool HasNoContentError { get; }
        public bool HasConflictError { get; }
        public bool HasNotGenericReturn { get; }
        public bool HasBadRequestError { get; }

        bool HasBusinessErrors { get; }
        bool HasBusinessWarnings { get; }
        bool HasBusinessInfo { get; }

        /// <summary>
        /// Creates a business error critic. 
        /// </summary>
        /// <param name="errorCode">Error code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on errorCode</param>
        void AddBusinessError(string errorCode, params object[] args);
        /// <summary>
        /// Gets the list of business error critics 
        /// </summary>
        /// <returns>List of business error critics</returns>
        public IEnumerable<ICritic> GetBusinessErrors();
        /// <summary>
        /// Creates a business warning critic. 
        /// </summary>
        /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on warningCode</param>
        void AddBusinessWarning(string warningCode, params object[] args);
        /// <summary>
        /// Gets the list of business warning critics 
        /// </summary>
        /// <returns>List of business warning critics</returns>
        public IEnumerable<ICritic> GetBusinessWarnings();
        /// <summary>
        /// Creates a business info critic. 
        /// </summary>
        /// <param name="infoCode">Info code (to be also localized in current culture)</param>
        /// <param name="args">List of parameters to expand inside localized message based on infoCode</param>
        void AddBusinessInfo(string infoCode, params object[] args);
        /// <summary>
        /// Gets the list of business info critics 
        /// </summary>
        /// <returns>List of business info critics</returns>
        public IEnumerable<ICritic> GetBusinessInfos();

        List<ICritic> Critics { get; }
        StatusCode StatusCode { get; set; }

        void ResetNoContentError();

        void ResetConflictError();
        bool HasCriticalErrors();
        Dictionary<string, object[]> GetCriticalErrors();
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}