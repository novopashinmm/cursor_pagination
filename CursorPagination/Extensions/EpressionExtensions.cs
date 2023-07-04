using System.Linq.Expressions;

namespace CursorPagination.Extensions;

public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right,
        bool addCondition)
    {
        return addCondition ? left.And(right) : left;
    }

    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var param = left.Parameters[0];
        if (ReferenceEquals(param, right.Parameters[0]))
        {
            return Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(left.Body, right.Body), param);
        }

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                left.Body,
                Expression.Invoke(right, param)), param);
    }

    public static Expression<Func<T, bool>> AndNot<T>(this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = left.Parameters[0];
        if (ReferenceEquals(param, right.Parameters[0]))
        {
            return Expression.Lambda<Func<T, bool>>(
                Expression.Not(Expression.AndAlso(left.Body, right.Body)), param);
        }

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                left.Body,
                Expression.Not(Expression.Invoke(right, param))), param);
    }

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> source)
    {
        return Expression.Lambda<Func<T, bool>>(Expression.Not(source.Body), source.Parameters);
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = left.Parameters[0];
        if (ReferenceEquals(param, right.Parameters[0]))
        {
            return Expression.Lambda<Func<T, bool>>(
                Expression.OrElse(left.Body, right.Body), param);
        }

        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(
                left.Body,
                Expression.Invoke(right, param)), param);
    }

    public static Expression<Func<T, bool>> CombineWithOr<T>(this IEnumerable<Expression<Func<T, bool>>> values)
    {
        return values.Aggregate((Expression<Func<T, bool>>)(x => false), (prev, next) => prev.Or(next));
    }

    public static Expression<Func<T, bool>> CombineWithAnd<T>(this IEnumerable<Expression<Func<T, bool>>> values)
    {
        return values.Aggregate((Expression<Func<T, bool>>)(x => true), (prev, next) => prev.And(next));
    }

    public static Expression<Func<TData, bool>> CreateFilter<TData, TKey>(string name, TKey valueToCompare)
    {
        var parameter = Expression.Parameter(typeof(TData));
        var expressionParameter = Expression.Property(parameter, name);

        var body = Expression.Equal(expressionParameter, Expression.Constant(valueToCompare, typeof(TKey)));
        return Expression.Lambda<Func<TData, bool>>(body, parameter);
    }
}