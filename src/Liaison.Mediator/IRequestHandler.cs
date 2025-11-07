using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Handles a request and produces a response.
/// </summary>
/// <typeparam name="TRequest">Type of the request message.</typeparam>
/// <typeparam name="TResponse">Type of the response returned by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the incoming request.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    /// <returns>The response for the request.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}
