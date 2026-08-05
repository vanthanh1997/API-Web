using MediatR;
namespace Application.Common.Interfaces
{
    public interface ICommandBase;
    public interface ICommand : IRequest, ICommandBase;
    public interface ICommand<out TResponse> : IRequest<TResponse>, ICommandBase;
}
