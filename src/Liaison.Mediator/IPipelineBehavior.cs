using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Defines a behavior that surrounds request handler execution.
/// </summary>
/// <typeparam name="TRequest">Type of the request.</typeparam>
/// <typeparam name="TResponse">Type of the response.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Executes code before and/or after the request handler.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="next">Delegate used to invoke the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Delegate representing the continuation to the next behavior or the request handler.
/// </summary>
/// <typeparam name="TResponse">Type of the response produced by the handler.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
