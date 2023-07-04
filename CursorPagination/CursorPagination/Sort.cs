using System.Linq.Expressions;
using CursorPagination.Enums;

namespace CursorPagination.CursorPagination;

public sealed class Sort<TSource, TSortField>
{
    public Expression<Func<TSource, TSortField>> ExpressionSort { get; set; }

    public SortType Type { get; set; }
}

public class Sort
{
    public SortColumn SortColumn { get; set; }

    public SortType SortType { get; set; }

    // public static Sort Map(GrpcCommonTypes.Sort requestSort)
    // {
    //     if (requestSort?.ColumnType != null)
    //     {
    //         return new Sort
    //         {
    //             SortColumn = (SortColumn)requestSort.ColumnType,
    //             SortType = (SortType)requestSort.SortType
    //         };
    //     }
    //
    //     return new Sort
    //     {
    //         SortColumn = SortColumn.Id,
    //         SortType = SortType.Ascending
    //     };
    // }
}