using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Liquid.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Implementation of the CRON-like scheduler inside a Microservice based on a centralized CRON dispatcher via MessageBus messages
    /// </summary>
    public abstract class LightJobScheduler : ILightWorker
    {
        protected readonly static Dictionary<MethodInfo, JobAttribute> Jobs = [];
        private readonly InputValidator inputValidator = new();

        public ICriticHandler CriticHandler => WorkBench.CriticHandler;
        public ILightTelemetry Telemetry => WorkBench.Telemetry;
        public ILightContext SessionContext => WorkBench.SessionContext;

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
        /// Discovery the key connection defined on the implementation of the LightJobWorker
        /// </summary>
        /// <param name="method">Method related the queue or topic</param>
        /// <returns>String key connection defined on the implementation of the LightJobWorker</returns>
        protected static string GetConnectionKey(MethodInfo method)
        {
            var attributes = method?.ReflectedType.CustomAttributes;
            string connectionKey = "";
            if (attributes.Any())
                connectionKey = attributes.ToArray()[0].ConstructorArguments[0].Value.ToString();
            return connectionKey;
        }

        /// <summary>
        /// Discovery of the subscription name as defined on the declaration of the LightJobScheduler
        /// </summary>
        /// <param name="method">Method related the scheduler</param>
        /// <returns>The subscription name</returns>
        protected static string GetSubscriptionName(MethodInfo method)
        {
            var attributes = method?.ReflectedType.CustomAttributes;
            string subscriptionName = "";
            if (attributes.Any())
                subscriptionName = attributes.ToArray()[0].ConstructorArguments[1].Value.ToString();
            return subscriptionName;
        }

        protected static int GetMaxConcurrentCalls(MethodInfo method)
        {
            var attributes = method?.ReflectedType.CustomAttributes;
            string maxConcurrentCalls = "";
            if (attributes.Any())
                maxConcurrentCalls = attributes.ToArray()[0].ConstructorArguments[2].Value.ToString();
            return Convert.ToInt32(maxConcurrentCalls);
        }

        /// <summary>
        /// Check if attribute was declared with Key Connection for the LightJobWorker
        /// </summary>
        /// <param name="method">Method related the queue or topic</param>
        /// <returns>Will true, if there is it</returns>
        private static bool IsDeclaredConnection(MethodInfo method)
        {
            return string.IsNullOrWhiteSpace(GetConnectionKey(method));
        }

        /// <summary>
        /// Get the method related the queue or topic
        /// </summary>
        /// <typeparam name="T">Type of the queue or topic</typeparam>
        /// <param name="item">Item related dictionary of queue or topic</param>
        /// <returns>Method related the queue or topic</returns>
        protected virtual MethodInfo GetMethod<T>(KeyValuePair<MethodInfo, T> item)
        {
            return item.Key;
        }

        /// <inheritdoc/>
        public virtual void Initialize()
        {
            WorkBench.ConsoleWriteLine("Starting Job Scheduler");
            Discovery();
        }

        /// <summary>
        /// Implementation of the start process to discovery by reflection the Worker
        /// </summary>
        protected static void Initialized()
        {
            WorkBench.ConsoleWriteLine("Job Scheduler started");
        }

        /// <summary>
        /// Method for discovery all methods that use a Job attribute.
        /// </summary>
        private static void Discovery()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<MethodInfo[]> _methodsSigned = (from assembly in assemblies
                                                 where !assembly.IsDynamic
                                                 from type in assembly.ExportedTypes
                                                 where type.BaseType is not null && type.BaseType == typeof(LightJobScheduler)
                                                 select type.GetMethods()).ToList();

            foreach (var methods in _methodsSigned)
                foreach (var method in methods)
                    foreach (JobAttribute job in method.GetCustomAttributes(typeof(JobAttribute), false).Cast<JobAttribute>())
                        if (!IsDeclaredConnection(method))
                        {
                            Jobs.Add(method, job);
                            RegisteredJobs.RegisterJob(method.Name, LightJobStatus.Running.Code);
                        }
                        else
                        {
                            // if there isn't Custom Attribute with string connection, will be throw exception.
                            throw new LightException($"No Attribute MessageBus with a configuration string has been informed on the worker \"{method.DeclaringType}\".");
                        }
        }

        /// <summary>
        /// Method created to process by reflection the Jobs declared
        /// </summary>
        /// <returns>True if job is found</returns>
        public static bool TryInvokeJob(JobDispatchMSG message)
        {
            // find registered job with method name that matches job name sent or ignores the invocation
            var job = Jobs.FirstOrDefault(j => j.Key.Name == message.Job);
            if (job.Equals(default(KeyValuePair<MethodInfo, JobAttribute>)))
                return false; //meaning notfound

            MethodInfo method = job.Key;
            if (message.CommandType == JobDispatchCMD.Abort.Code)
                RegisteredJobs.UpdateJobStatus(message.Job, LightJobStatus.Aborted.Code);
            else
            {
                //Check if it needs authorization, unless there isn't AuthorizeAttribute
                foreach (AuthorizeAttribute authorize in method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>())
                {
                    if (message.TransactionContext?.User is null)
                        //If there isn't Context, should throw exception.
                        throw new LightException("No TokenJwt has been informed on the message sent to the worker.");
                    if ((authorize.Policy is not null) && (message.TransactionContext.User.FindFirst(authorize.Policy) is null))
                        throw new LightException($"No Policy \"{authorize.Policy}\" has been informed on the message sent to the worker.");
                    if ((authorize.Roles is not null) && (!message.TransactionContext.User.IsInRole(authorize.Roles)))
                        throw new LightException($"No Roles \"{authorize.Roles}\" has been informed on the message sent to the worker.");
                }

                try
                {
                    object lightJobWorker = Activator.CreateInstance(method.ReflectedType, null);

                    WorkBench.SetSession(((ILightMessage)message).TransactionContext, new CriticHandler());

                    object[] parametersArray = [message.Activation, message.Partition];
                    method.Invoke(lightJobWorker, parametersArray);

                }
                catch (Exception e)
                {
                    var lightEx = new LightException($"Error trying to invoke '{method.ReflectedType}' of microservice '{message.Microservice}' with parms {{ Activation: {message.Activation}, Partition: {message.Partition} }}", e);
                    WorkBench.BaseTelemetry.TrackException(lightEx);
                    throw lightEx;
                }
            }

            return true;
        }

        /// <summary>
        /// Method for create a instance of LightDomain objects
        /// </summary>
        /// <typeparam name="T">Type of LightDomain</typeparam>
        /// <returns></returns>
        protected T Factory<T>() where T : LightDomain, new()
        {
            // Throws errors as a specific exception 
            if (inputValidator.ErrorsCount > 0)
                throw new InvalidInputLightException(inputValidator.Errors);

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

        /// <summary>
        /// Termites and responds the caller
        /// </summary>
        protected void Terminate()
        {
            //Verify if there's errors
            if (!MessageBusInterceptor.ShouldInterceptMessages && CriticHandler.HasCriticalErrors())
            {
                // Throws the error code from errors list of input validation to View Model
                throw new BusinessValidationLightException(CriticHandler.GetCriticalErrors());
            }
            else if (!MessageBusInterceptor.ShouldInterceptMessages && CriticHandler.HasBusinessWarnings)
            {
                Telemetry.TrackException(new LightBusinessWarningException(CriticHandler.Critics.Where(c => c.Type == CriticType.Warning).ToJsonString()));
            }
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}