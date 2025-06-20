using MemberPropertyAlert.Core.Common;

namespace MemberPropertyAlert.Core.Application.Commands
{
    /// <summary>
    /// Marker interface for commands (CQRS pattern)
    /// </summary>
    public interface ICommand
    {
    }

    /// <summary>
    /// Interface for commands that return a result
    /// </summary>
    /// <typeparam name="TResult">The type of result returned</typeparam>
    public interface ICommand<TResult> : ICommand
    {
    }

    /// <summary>
    /// Interface for command handlers
    /// </summary>
    /// <typeparam name="TCommand">The command type</typeparam>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        Task<Result> HandleAsync(TCommand command);
    }

    /// <summary>
    /// Interface for command handlers that return a result
    /// </summary>
    /// <typeparam name="TCommand">The command type</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        Task<Result<TResult>> HandleAsync(TCommand command);
    }
}
