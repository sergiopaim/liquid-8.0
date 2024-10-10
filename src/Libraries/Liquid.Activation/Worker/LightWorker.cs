using Liquid.Base;
using Liquid.Domain;
using Liquid.Domain.Test;
using Liquid.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Implementation of the communication component between queues and topics, 
    /// to carry out the good practice of communication
    /// between micro services. In order to use this feature it is necessary 
    /// to implement the inheritance of this class.
    /// </summary>
    public abstract class LightWorker : ILightWorker
    {
        protected readonly static Dictionary<MethodInfo, QueueAttribute> Queues = [];
        protected readonly static Dictionary<MethodInfo, TopicAttribute> Topics = [];

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
        /// Discovery the key connection defined on the implementation of the LightWorker
        /// </summary>
        /// <param name="method">Method related the queue or topic</param>
        /// <returns>String key connection defined on the implementation of the LightWorker</returns>
        protected static string GetKeyConnection(MethodInfo method)
        {
            var attributes = method?.ReflectedType.CustomAttributes;
            string connectionKey = "";
            if (attributes.Any())
                connectionKey = attributes.ToArray()[0].ConstructorArguments[0].Value.ToString();
            return connectionKey;
        }

        /// <summary>
        /// Check if it was declared attribute of the Key Connection on the implementation of the LightWorker
        /// </summary>
        /// <param name="method">Method related the queue or topic</param>
        /// <returns>Will true, if there is it</returns>
        private static bool IsDeclaredConnection(MethodInfo method)
        {
            return string.IsNullOrWhiteSpace(GetKeyConnection(method));
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
            Discovery();
        }

        /// <summary>
        /// Method for discovery all methods that use a LightQueue or LightTopic.
        /// </summary>
        private static void Discovery()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<MethodInfo[]> _methodsSigned = (from assembly in assemblies
                                                 where !assembly.IsDynamic
                                                 from type in assembly.ExportedTypes
                                                 where type.BaseType is not null && type.BaseType == typeof(LightWorker)
                                                 select type.GetMethods()).ToList();

            foreach (var methods in _methodsSigned)
                foreach (var method in methods)
                {
                    foreach (TopicAttribute topic in (TopicAttribute[])method.GetCustomAttributes(typeof(TopicAttribute), false))
                        if (!IsDeclaredConnection(method))
                            if (Topics.Values.FirstOrDefault(x => x.TopicName == topic.TopicName && x.Subscription == topic.Subscription) is null)
                                Topics.Add(method, topic);
                            else
                                throw new LightException($"Duplicated worker: there's already a worker for the same topic (\"{topic.TopicName}\") and subscription(\"{topic.Subscription}\")");
                        // if there isn't Custom Attribute with string connection, will be throw exception.
                        else
                            throw new LightException($"No Attribute MessageBus with a configuration string has been informed on the worker \"{method.DeclaringType}\".");

                    foreach (QueueAttribute queue in (QueueAttribute[])method.GetCustomAttributes(typeof(QueueAttribute), false))
                        if (!IsDeclaredConnection(method))
                            if (Queues.Values.FirstOrDefault(x => x.QueueName == queue.QueueName) is null)
                                Queues.Add(method, queue);
                            else
                                throw new LightException($"There is already Queue defined with the name \"{queue.QueueName}\".");
                        //If there isn't Custom Attribute with string connection, will be throw exception.
                        else
                            throw new LightException($"No Attribute MessageBus with a configuration string has been informed on the worker \"{method.DeclaringType}\".");
                }
        }

        /// <summary>
        /// Method created to process by reflection the Workers declared
        /// </summary>
        /// <returns>object</returns>
        public static object InvokeWorker(MethodInfo method, BinaryData message)
        {
            object result = null;
            if (method is not null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                object lightWorker = Activator.CreateInstance(method.ReflectedType, null);

                if (parameters.Length == 0)
                    result = method.Invoke(lightWorker, null);
                else
                {
                    dynamic lightMessage = JsonSerializer.Deserialize(Encoding.UTF8.GetString(message), parameters[0].ParameterType, LightGeneralSerialization.IgnoreCase);
                    //Check if it needs authorization, unless that there isn't AuthorizeAttribute
                    foreach (AuthorizeAttribute authorize in (AuthorizeAttribute[])method.GetCustomAttributes(typeof(AuthorizeAttribute), false))
                    {
                        //If there isn't Context, will be throw exception.
                        if ((lightMessage.Context is null) || ((lightMessage.Context is not null) && (lightMessage.Context.User is null)))
                            throw new LightException("No TokenJwt has been informed on the message sent to the worker.");

                        if ((authorize.Policy is not null) && (lightMessage.Context.User.FindFirst(authorize.Policy) is null))
                            throw new LightException($"No Policy \"{authorize.Policy}\" has been informed on the message sent to the worker.");

                        if ((authorize.Roles is not null) && (!lightMessage.Context.User.IsInRole(authorize.Roles)))
                            throw new LightException($"No Roles \"{authorize.Roles}\" has been informed on the message sent to the worker.");

                    }

                    WorkBench.SetSession(((ILightMessage)lightMessage).TransactionContext, new CriticHandler());

                    object[] parametersArray = [lightMessage];
                    result = method.Invoke(lightWorker, parametersArray);
                }

                if (result is not null)
                {
                    var resultTask = (result as System.Threading.Tasks.Task);
                    resultTask.Wait();
                    if (resultTask.IsFaulted)
                    {
                        resultTask.Exception?.FilterRelevantStackTrace();
                        throw resultTask.Exception;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Method for create a instance of LightDomain objects
        /// </summary>
        /// <typeparam name="T">Type of LightDomain</typeparam>
        /// <returns></returns>
        protected T Factory<T>() where T : LightDomain, new()
        {
            // Prevents the request execution and throws errors as a specific exception
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