namespace CursorPagination.Interfaces;

public interface IAuditableObject
{
    public long Id { get; }

    DateTime Created { get; set; }
}