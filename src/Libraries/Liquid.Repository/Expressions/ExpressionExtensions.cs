using System;
using System.Linq.Expressions;

namespace Liquid.Repository
{
    /// <summary>
    /// Classes responsible to update parameters
    /// </summary>
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Class responsible to update parameters
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="oldParameter"></param>
        /// <param name="newParameter"></param>
        private class RebindParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter) : ExpressionVisitor
        {
            /// <summary>
            /// Verify parameter
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == oldParameter)
                    return newParameter;

                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Where clause to aggregate lambda expressions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expr1"></param>
        /// <param name="expr2"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> Where<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
        {
            var expr2Body = new RebindParameterVisitor(expr2?.Parameters[0], expr1?.Parameters[0]).Visit(expr2?.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1?.Body, expr2Body), expr1?.Parameters);
        }

        /// <summary>
        /// Where clause to aggregate lambda expressions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expr1"></param>
        /// <param name="expr2"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> WhereOr<T>(this Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
        {
            var expr2Body = new RebindParameterVisitor(expr2?.Parameters[0], expr1?.Parameters[0]).Visit(expr2?.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.Or(expr1?.Body, expr2Body), expr1?.Parameters);
        }
    }
}