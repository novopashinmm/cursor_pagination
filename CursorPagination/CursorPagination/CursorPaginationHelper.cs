using System.Linq.Expressions;
using System.Reflection;
using CursorPagination.Enums;
using CursorPagination.Interfaces;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using CursorPagination.Extensions;

namespace CursorPagination.CursorPagination;

public static class CursorPaginationHelper
{
    public static async Task<(ICollection<TProjection> items, bool hasNext)> GetReturnsWithoutOffsetProjections<TProjection, TObject,
        TProperty>(
        IQueryable<TObject> queryable,
        Expression<Func<TObject, TProperty>> propertyPath,
        TProperty value,
        Expression<Func<TObject, bool>> filter,
        Expression<Func<TObject, TProjection>> map,
        CursorPaginationRequest cursorPagination,
        CancellationToken cancellationToken)
        where TProperty : struct
        where TObject : IAuditableObject
    {
        filter = filter.And(GetCursorFilter(propertyPath, value, cursorPagination));

        var filteredData = queryable.Where(filter);
        var sortedData = SortAscDesc(propertyPath);
        sortedData = cursorPagination.Direction == CursorPaginationRequest.DirectionType.Next
            ? sortedData.ThenBy(x => x.Id)
            : sortedData.ThenByDescending(x => x.Id);

        var query = sortedData
            .Select(map)
            .Take(cursorPagination.Limit + 1);

        var result = await query
            .ToListAsync(cancellationToken);

        var hasNext = result.Count - cursorPagination.Limit == 1;

        result = result.Take(cursorPagination.Limit).ToList();

        if (cursorPagination.IsNeedReverse())
        {
            result.Reverse();
        }

        return (result, hasNext);

        IOrderedQueryable<TObject> SortAscDesc<T>(Expression<Func<TObject, T>> getField)
        {
            return cursorPagination.IsSameDirectionAndSort()
                ? filteredData.OrderBy(getField)
                : filteredData.OrderByDescending(getField);
        }
    }

    public static Expression<Func<TObject, bool>> GetCursorFilter<TObject, TProperty>(
        Expression<Func<TObject, TProperty>> propertyPath,
        TProperty value,
        CursorPaginationRequest pagination)
        where TProperty : struct
        where TObject : IAuditableObject
    {
        if (pagination.Column is null or SortColumn.Id)
        {
            return GetCompareLambda(propertyPath, value, pagination.IsSameDirectionAndSort());
        }

        return GeneratePaginationCompareLambda(propertyPath, value, pagination);
    }

    public static Expression<Func<T, bool>> GeneratePaginationCompareLambda<T, TProperty>
    (Expression<Func<T, TProperty>> property,
        TProperty value,
        CursorPaginationRequest pagination)
        where T : IAuditableObject
        where TProperty : struct
    {
        if (pagination.Value is null)
        {
            return z => true;
        }

        var (compareValue, equalsValue, compareId) =
            GeneratePaginationCompareLambdas(property, value, pagination);

        return compareValue.Or(equalsValue.And(compareId));
    }

    private static (
        Expression<Func<TEntity, bool>> compareValueLambda,
        Expression<Func<TEntity, bool>> equalValueLambda,
        Expression<Func<TEntity, bool>> compareIdLambda)
        GeneratePaginationCompareLambdas<TEntity, TProperty>(
            Expression<Func<TEntity, TProperty>> property,
            TProperty value,
            CursorPaginationRequest pagination)
        where TProperty : struct
        where TEntity : IAuditableObject
    {
        var compareValueLambda = GetCompareLambda(property, value, pagination.IsSameDirectionAndSort());
        var equalValueLambda = GetEqualsLambda(property, value);
        var compareIdLambda = GetCompareLambda<TEntity, long>(x => x.Id, pagination.Id ?? 0,
            pagination.Direction == CursorPaginationRequest.DirectionType.Next);

        return (compareValueLambda, equalValueLambda, compareIdLambda);
    }

    public static Expression<Func<TEntity, bool>> GetEqualsLambda<TEntity, TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value) where TProperty : struct
    {
        var left = property.Body;
        Expression right = Expression.Constant(value, typeof(TProperty));

        return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(left, right),
            new ParameterExpression[] { property.Parameters.Single() });
    }

    public static Expression<Func<TEntity, bool>> GetCompareLambda<TEntity, TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value,
        bool isGreater) where TProperty : struct
    {
        var left = property.Body;
        Expression right = Expression.Constant(value, typeof(TProperty));

        Expression searchExpression = null;
        if (isGreater)
        {
            searchExpression = Expression.GreaterThan(left, right);
        }
        else
        {
            searchExpression = Expression.LessThan(left, right);
        }

        return Expression.Lambda<Func<TEntity, bool>>(searchExpression,
            new ParameterExpression[] { property.Parameters.Single() });
    }

    public static T TryParse<T>(object value, SortType sortType, CursorPaginationRequest.DirectionType directionType)
    {
        try
        {
            if (!typeof(T).IsClass && value is null)
            {
                return GetDefaultValue<T>(sortType, directionType);
            }

            object castValue = value;

            if (typeof(T) == typeof(DateTimeOffset) && value?.GetType() == typeof(Timestamp))
            {
                castValue = ((Timestamp)value).ToDateTimeOffset();
            }

            if (typeof(T) == typeof(DateTime) && value?.GetType() == typeof(Timestamp))
            {
                castValue = ((Timestamp)value).ToDateTime();
            }



            var result = (T)castValue;
            return result;
        }
        catch (NotSupportedException)
        {
            throw new ArgumentException($"Wrong Pagination.Value for value={value} and typeof={typeof(T)}");
        }
    }

    private static T GetDefaultValue<T>(SortType sortType, CursorPaginationRequest.DirectionType directionType)
    {
        if (sortType == SortType.Ascending && directionType == CursorPaginationRequest.DirectionType.Next)
        {
            if (typeof(T) == typeof(long)
                || typeof(T) == typeof(double)
                || typeof(T) == typeof(DateTime)
                || typeof(T) == typeof(DateTimeOffset))
            {
                return default;
            }
        }
        else if (sortType == SortType.Descending && directionType == CursorPaginationRequest.DirectionType.Next)
        {
            FieldInfo maxValueField = typeof(T).GetField("MaxValue", BindingFlags.Public
                                                                     | BindingFlags.Static);
            if (maxValueField == null)
                throw new NotSupportedException(typeof(T).Name);
            T maxValue = (T)maxValueField.GetValue(null);
            return maxValue;
        }
        throw new RpcException(new Status(StatusCode.InvalidArgument, "При открытии 1 страницы таблицы значение DirectionType может быть только Next"));
    }
}