using CursorPagination.Enums;
using Ozon.Rms.SellerReturns.Grpc.SellerReturnsCommonTypes;
using SortType = CursorPagination.Enums.SortType;

namespace CursorPagination.CursorPagination;

public class CursorPaginationRequest
{
    public long? Id { get; set; }
    public SortColumn? Column { get; set; }
    public object Value { get; set; }
    public SortType SortType { get; set; }
    public int Limit { get; set; }
    public DirectionType Direction { get; set; }

    // получается у нас есть 4 степени свободы, сортировка по value, сортировка по id, > или < по value, > или < по id
    // чтобы не терять записи при пагинации нам надо воздействовать на все эти степени свободы
    // этот метод воздействует на > или < по value, а также на сортировку по value
    // на оставшиеся степени свободы по id воздействует DirectionType
    public bool IsSameDirectionAndSort()
    {
        return (SortType == SortType.Ascending
                && Direction == DirectionType.Next)
               ||
               (SortType == SortType.Descending
                && Direction == DirectionType.Previous);
    }

    // если IsSameDirectionOrder == true значит применили сортировку asc а в запросе пришел desc, значит нужен reverse
    // если IsSameDirectionOrder == false значит применили сортировку desc а в запросе пришел asc, значит нужен reverse
    public bool IsNeedReverse()
    {
        var isSameDirectionSort = IsSameDirectionAndSort();
        return (isSameDirectionSort && SortType == SortType.Descending)
               || (!isSameDirectionSort && SortType == SortType.Ascending);
    }

    public static CursorPaginationRequest Map(Pagination requestPagination,
        Sort mappedSort)
    {
        CursorPaginationRequest pagination;
        var defaultLimit = 1000;

        if (requestPagination == null)
        {
            pagination = new CursorPaginationRequest
            {
                Id = default,
                Limit = defaultLimit,
                Column = mappedSort.SortColumn,
                SortType = mappedSort.SortType,
                Direction = DirectionType.Next,
                Value = null
            };
        }
        else
        {
            object value = requestPagination.ValueCase switch
            {
                Pagination.ValueOneofCase.LongValue => requestPagination.LongValue,
                Pagination.ValueOneofCase.DateTimeValue => requestPagination.DateTimeValue,
                Pagination.ValueOneofCase.DoubleValue => requestPagination.DoubleValue,
                Pagination.ValueOneofCase.None => null,
                _ => throw new ArgumentException($"{requestPagination.ValueCase}")
            };
            pagination = new CursorPaginationRequest
            {
                Id = requestPagination.Id,
                Limit = requestPagination.Limit,
                Column = mappedSort.SortColumn,
                SortType = mappedSort.SortType,
                Direction = (DirectionType)requestPagination.DirectionType,
                Value = value
            };
        }

        return pagination;
    }

    public enum DirectionType
    {
        Next = 0,
        Previous = 1
    }
}