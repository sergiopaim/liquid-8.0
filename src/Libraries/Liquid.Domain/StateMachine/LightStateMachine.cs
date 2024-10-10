using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Liquid.Domain;
/// <summary>
/// Class responsible for representing business logic as a state machine
/// </summary>
/// <typeparam name="TModel">The LightModel class that is attached to the state machine</typeparam>
/// <typeparam name="TState">The LightEnum class that contains the states the machine will have</typeparam>
/// <typeparam name="TEvent">The enum that contains the events the machine will handle</typeparam>
/// <param name="model">The LightModel instance to have its state controlled</param>
public abstract class LightStateMachine<TModel, TState, TEvent>(TModel model) where TModel : ILightModel
                                                                              where TState : ILightEnum
                                                                              where TEvent : Enum
{
    private readonly Dictionary<TState, List<TransitionMapping>> transitions = [];
    private TState currentState;
    private Func<TModel, TState> getState;
    private Action<TModel, TState> setState;
    private Action<TModel, TState, TEvent> errorHandler;

    #region Context properties and methods

    /// <summary>
    /// The critic handler
    /// </summary>
    public static ICriticHandler CriticHandler => WorkBench.CriticHandler;

    /// <summary>
    /// The current active telemetry service
    /// </summary>
    public static ILightTelemetry Telemetry => WorkBench.Telemetry;

    /// <summary>
    /// Current session context
    /// </summary>
    public static ILightContext SessionContext => WorkBench.SessionContext;

    /// <summary>
    /// Gets the id of the current user
    /// </summary>
    public static string CurrentUserId => SessionContext.CurrentUserId;

    /// <summary>
    /// Gets the first name of the current user
    /// </summary>
    public static string CurrentUserFirstName => SessionContext.CurrentUserFirstName;

    /// <summary>
    /// Gets the full name of the current user
    /// </summary>
    public static string CurrentUserFullName => SessionContext.CurrentUserFirstName;

    /// <summary>
    /// Gets the e-mail address of the current user
    /// </summary>
    public static string CurrentUserEmail => SessionContext.CurrentUserEmail;

    /// <summary>
    /// Checks if the current user is in the given security role
    /// </summary>
    /// <param name="role">Security role</param>
    /// <returns>True if the user is in the role</returns>
    public static bool CurrentUserIsInRole(string role) => SessionContext.CurrentUserIsInRole(role);

    /// <summary>
    /// Checks if the current user is in any of the given security roles
    /// </summary>
    /// <param name="roles">Security roles in a comma separated string</param>
    /// <returns>True if the user is in any role</returns>
    public static bool CurrentUserIsInAnyRole(string roles) => SessionContext.CurrentUserIsInAnyRole(roles);

    /// <summary>
    /// Checks if the current user is in any of the given security roles
    /// </summary>
    /// <param name="roles">List of security roles</param>
    /// <returns>True if the user is in any role</returns>
    public static bool CurrentUserIsInAnyRole(params string[] roles) => SessionContext.CurrentUserIsInAnyRole(roles);

    /// <summary>
    /// Indicates whether at least one Business error has been issued
    /// </summary>
    public static bool HasBusinessErrors => CriticHandler.HasBusinessErrors;

    #region AddBusinessError

    /// <summary>
    /// Method add the error code to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="errorCode">Error code (to be also localized in current culture)</param>
    public static void AddBusinessError(string errorCode)
    {
        CriticHandler.AddBusinessError(errorCode);
    }

    /// <summary>
    /// Method add the error code to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="errorCode">error code</param>
    /// <param name="message">error message</param>
    public static void AddBusinessError(string errorCode, string message)
    {
        CriticHandler.AddBusinessError(errorCode, [message]);
    }

    /// <summary>
    /// Method add the error code to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="errorCode">Error code (to be also localized in current culture)</param>
    /// <param name="args">List of parameters to expand inside localized message based on errorCode</param>
    public static void AddBusinessError(string errorCode, params object[] args)
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
    public static void AddBusinessWarning(string warningCode)
    {
        CriticHandler.AddBusinessWarning(warningCode);
    }

    /// <summary>
    /// Method add the warning to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
    /// <param name="message">error message</param>
    public static void AddBusinessWarning(string warningCode, string message)
    {
        CriticHandler.AddBusinessWarning(warningCode, [message]);
    }

    /// <summary>
    /// Method add the error code to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="warningCode">Warning code (to be also localized in current culture)</param>
    /// <param name="args">List of parameters to expand inside localized message based on warningCode</param>
    public static void AddBusinessWarning(string warningCode, params object[] args)
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
    public static void AddBusinessInfo(string infoCode)
    {
        CriticHandler.AddBusinessInfo(infoCode);
    }

    /// <summary>
    /// /// Method add the information to the Critic Handler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="infoCode">Info code (to be also localized in current culture)</param>
    /// <param name="message">error message</param>
    public static void AddBusinessInfo(string infoCode, string message)
    {
        CriticHandler.AddBusinessInfo(infoCode, [message]);
    }

    /// <summary>
    /// Method add the error code to the CriticHandler
    /// and add in Critics list to build the object InvalidInputLightException
    /// </summary>
    /// <param name="infoCode">Info code (to be also localized in current culture)</param>
    /// <param name="args">List of parameters to expand inside localized message based on infoCode</param>
    public static void AddBusinessInfo(string infoCode, params object[] args)
    {
        CriticHandler.AddBusinessInfo(infoCode, args);
    }

    #endregion

    #endregion

    /// <summary>
    /// Defines the function to get the state from the model
    /// </summary>
    /// <param name="state">The function to get the state from the model</param>
    protected void StateGetter(Func<TModel, TState> state)
    {
        getState = state;
    }

    /// <summary>
    /// Defines the action to handle the state transition
    /// </summary>
    /// <param name="action">The action to handle the state transition</param>
    protected void StateSetter(Action<TModel, TState> action)
    {
        setState = action;
        SetCurrentState();
    }

    /// <summary>
    /// Defines the standard action to be triggered when a transition check fails
    /// </summary>
    /// <param name="standard">The standard action to be triggered when a transition check fails</param>
    protected void ErrorHandler(Action<TModel, TState, TEvent> standard)
    {
        errorHandler = standard;
    }

    public List<TEvent> Events => Enum.GetValues(typeof(TEvent)).Cast<TEvent>().ToList();
    public List<TState> All => CurrentState.ListAll().Select(s => (TState)s).ToList();
    public TState CurrentState => currentState;

    /// <summary>
    /// Adds a transition to the state machine
    /// </summary>
    /// <param name="fromState">State to transition from</param>
    /// <param name="toState">State to transition to</param>
    /// <param name="trigger">The event that triggers the transition</param>
    /// <param name="standardAction">The standard action to be triggered if the transition happens</param>
    /// <exception cref="LightException">Exception throw if conflicting transtitions are found.</exception>
    protected void Transition(TState fromState, TState toState, TEvent trigger, Action<TModel> standardAction = null)
    {
        if (!transitions.TryGetValue(fromState, out List<TransitionMapping> value))
        {
            value = [];
            transitions[fromState] = value;
        }

        if (value.Any(v => v.ToState.Code == toState.Code &&
                           v.Trigger.Equals(trigger)))
            throw new LightException($"There already is a transition '{fromState.Code} > {toState.Code} | {trigger}'");

        value.Add(new TransitionMapping(toState, trigger, standardAction));
    }

    private void SetCurrentState(TState newState = default)
    {
        newState ??= GetCurrentState();

        if (setState is null)
            throw new LightException("SetState action is not defined. Define GetState first than SetState.");

        setState.Invoke(model, newState);
        currentState = newState;
    }

    private TState GetCurrentState()
    {
        if (getState is null)
            throw new LightException("GetState action is not defined. Define GetState first than SetState.");

        return getState.Invoke(model);
    }

    /// <summary>
    /// Process a state change event
    /// </summary>
    /// <param name="trigger">The event</param>
    /// <param name="specificAction">The specific action to be triggered. If ommited, the standard action, if defined, will be triggered.</param>
    /// <returns>True if success. False if any business error has been added.</returns>
    /// <exception cref="LightException">Thrown if the transition is not allowed</exception>
    public bool Process(TEvent trigger, Action<TModel> specificAction = null)
    {
        var mapping = FindMapping(CurrentState, trigger) ?? throw new LightException($"Invalid StateMachine transition: event '{trigger}' from state '{CurrentState.Code}'");

        if (HasBusinessErrors)
            return false;

        var old = currentState;

        SetCurrentState(mapping.ToState);

        specificAction ??= mapping.Action;

        specificAction?.Invoke(model);

        if (HasBusinessErrors)
        {
            SetCurrentState(old);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a state change event can be processed
    /// </summary>
    /// <param name="trigger">The event</param>
    /// <param name="specificErrorHandler">The specific action to be triggered when the transition check fails. If ommited, the standard errorHandler action, if defined, will be triggered.</param>
    /// <returns>True if the transition is allowed. False otherwise</returns>
    public bool Check(TEvent trigger, Action<TModel, TState, TEvent> specificErrorHandler = null)
    {
        var check = FindMapping(CurrentState, trigger) is not null;

        if (!check)
            (specificErrorHandler ?? errorHandler)?.Invoke(model, CurrentState, trigger);

        return check;
    }

    /// <summary>
    /// Checks if a state change event cannot be processed
    /// </summary>
    /// <param name="trigger">The event</param>
    /// <param name="specificErrorHandler">The specific action to be triggered when the transition check fails. If ommited, the standard errorHandler action, if defined, will be triggered.</param>
    /// <returns>False if the transition is allowed. True otherwise</returns>
    public bool DoesntCheck(TEvent trigger, Action<TModel, TState, TEvent> specificErrorHandler = null)
    {
        return !Check(trigger, specificErrorHandler);
    }

    private TransitionMapping FindMapping(TState state, TEvent trigger)
    {
        transitions.TryGetValue(state, out List<TransitionMapping> mappings);

        var mapping = mappings?.FirstOrDefault(m => m.Trigger.Equals(trigger));

        return mapping;
    }

    private class TransitionMapping(TState toState, TEvent trigger, Action<TModel> action)
    {
        public TState ToState { get; } = toState;
        public TEvent Trigger { get; } = trigger;
        public Action<TModel> Action { get; } = action;
    }
}