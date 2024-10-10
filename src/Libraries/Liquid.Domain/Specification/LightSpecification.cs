using LinqKit;
using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Linq.Expressions;

namespace Liquid.Domain
{
    public class LightSpecification<T> : ILightSpecification<T>
    {
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

        private readonly Expression<Func<T, bool>> expression;
        private Func<T, bool> compiled;
        /// <summary>
        /// Indication whether the clause is being used to do a linq query (or, else, to do a simple match).
        /// </summary>
        protected bool IsQuerying { get; private set; } = false;

        /// <summary>
        /// New empty specification, to be defined by the overriding the GetClause() method
        /// </summary>
        public LightSpecification() { }
        /// <summary>
        /// New specification defined as an Linq expression
        /// </summary>
        /// <param name="expression">The Linq expression</param>
        /// <exception cref="ArgumentNullException">If expression is null</exception>
        public LightSpecification(Expression<Func<T, bool>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression), "Invalid expression");
            else
                this.expression = expression;
        }
        /// <summary>
        /// Returns the specification as a Linq where clause
        /// </summary>
        /// <returns>The linq where clause</returns>
        public virtual ExpressionStarter<T> WhereClause
        {
            get
            {
                var toReturn = expression;

                if (toReturn is null)
                {
                    IsQuerying = true;
                    toReturn = GetClause();
                    IsQuerying = false;
                }

                if (toReturn is not null)
                    return NewClause(toReturn);
                else
                    throw new LightException("No expression was defined neither by a constructor nor by the overriding the GetClause() method");
            }
        }
        /// <summary>
        /// Checks if the instance matches (satisfies) the policy specification
        /// </summary>
        /// <param name="instance">The instance to evaluate the match</param>
        /// <returns>True if matches</returns>
        public virtual bool Matches(T o)
        {
            if (compiled is null)
            {
            var toCompile = expression ?? GetClause();

            if (toCompile is not null)
                    compiled = toCompile.Compile();
            else
                throw new LightException("No expression was defined neither by a constructor nor by the overriding the GetClause() method");
        }

            return compiled(o);
        }
        /// <summary>
        /// Compose the current specification and the one as parameter with an AND operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        public ILightSpecification<T> And(ILightSpecification<T> specification)
        {
            return new LightSpecificationAND<T>(this, specification);
        }
        /// <summary>
        /// Compose the current specification and the one as parameter with an OR operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        public ILightSpecification<T> Or(ILightSpecification<T> specification)
        {
            return new LightSpecificationOR<T>(this, specification);
        }
        /// <summary>
        /// Returns the current specification with a NOT operator
        /// </summary>
        /// <returns>The composed specification</returns>
        public ILightSpecification<T> Not()
        {
            return new LightSpecificationNOT<T>(this);
        }
        /// <summary>
        /// Compose the current specification and negating the one as parameter with an NOT operator
        /// </summary>
        /// <param name="specification">The specification to compose</param>
        /// <returns>The composed specification</returns>
        public ILightSpecification<T> Not(ILightSpecification<T> specification)
        {
            return new LightSpecificationNOT<T>(this, specification);
        }
        /// <summary>
        /// Returns the linq expression clause
        /// </summary>
        /// <returns>The linq expression clause</returns>
        protected virtual ExpressionStarter<T> GetClause()
        {
            return null;
        }
        /// <summary>
        /// Start an where clause
        /// </summary>
        /// <returns>The new where clause.</returns>
        protected ExpressionStarter<T> NewClause() => PredicateBuilder.New<T>();
        /// <summary>
        /// Start an where clause
        /// </summary>
        /// <param name="expression">Expression to be used when starting the where clause.</param>
        /// <returns>The new where clause.</returns>
        protected ExpressionStarter<T> NewClause(Expression<Func<T, bool>> expression) => PredicateBuilder.New(expression);
        /// <summary>
        /// Start an where clause with a stub expression true or false 
        /// </summary>
        /// <param name="expression">The stub value.</param>
        /// <returns>The new where clause.</returns>
        protected ExpressionStarter<T> NewClause(bool defaultExpression) => PredicateBuilder.New<T>(defaultExpression);
    }

    internal class LightSpecificationAND<T>(ILightSpecification<T> left, ILightSpecification<T> right) : LightSpecification<T>
    {
        public override bool Matches(T o) => left.Matches(o) && right.Matches(o);
        public override ExpressionStarter<T> WhereClause => left.WhereClause.And(right.WhereClause);
    }

    internal class LightSpecificationOR<T>(ILightSpecification<T> left, ILightSpecification<T> right) : LightSpecification<T>
    {
        public override bool Matches(T o) => left.Matches(o) || right.Matches(o);
        public override ExpressionStarter<T> WhereClause => left.WhereClause.Or(right.WhereClause);
    }

    internal class LightSpecificationNOT<T> : LightSpecification<T>
    {
        private readonly ILightSpecification<T> and;
        private readonly ILightSpecification<T> not;
        public LightSpecificationNOT(ILightSpecification<T> not)
        {
            this.not = not;
        }

        public LightSpecificationNOT(ILightSpecification<T> and, ILightSpecification<T> not)
        {
            this.and = and;
            this.not = not;
        }

        public override bool Matches(T o) => (and is null || and.Matches(o)) && !not.Matches(o);
        public override ExpressionStarter<T> WhereClause
        {
            get
            {
                var negation = not.WhereClause.Not();
                if (and is null)
                    return negation;
                else
                    return and.WhereClause.And(negation);
            }
        }
    }
}