using MemberPropertyAlert.Core.Common;

namespace MemberPropertyAlert.Core.Application.Queries
{
    /// <summary>
    /// Marker interface for queries (CQRS pattern)
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query</typeparam>
    public interface IQuery<TResult>
    {
    }

    /// <summary>
    /// Interface for query handlers
    /// </summary>
    /// <typeparam name="TQuery">The query type</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
    {
        Task<Result<TResult>> HandleAsync(TQuery query);
    }
}
