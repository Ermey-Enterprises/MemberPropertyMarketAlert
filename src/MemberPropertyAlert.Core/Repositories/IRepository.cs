using MemberPropertyAlert.Core.Common;

namespace MemberPropertyAlert.Core.Repositories
{
    /// <summary>
    /// Base repository interface for common CRUD operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IRepository<T> where T : class
    {
        Task<Result<T>> GetByIdAsync(string id);
        Task<Result<T>> CreateAsync(T entity);
        Task<Result<T>> UpdateAsync(T entity);
        Task<Result> DeleteAsync(string id);
        Task<Result<bool>> ExistsAsync(string id);
    }

    /// <summary>
    /// Repository interface for read-only operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IReadOnlyRepository<T> where T : class
    {
        Task<Result<T>> GetByIdAsync(string id);
        Task<Result<bool>> ExistsAsync(string id);
    }

    /// <summary>
    /// Repository interface for entities that support querying
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IQueryableRepository<T> : IRepository<T> where T : class
    {
        Task<Result<IEnumerable<T>>> GetAllAsync();
        Task<Result<IEnumerable<T>>> FindAsync(Func<T, bool> predicate);
        Task<Result<T>> FirstOrDefaultAsync(Func<T, bool> predicate);
        Task<Result<int>> CountAsync();
        Task<Result<int>> CountAsync(Func<T, bool> predicate);
    }
}
