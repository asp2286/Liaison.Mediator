using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Coordinates the dispatching of requests and notifications to their handlers.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request to the configured handler.
    /// </summary>
    /// <typeparam name="TResponse">Type of response expected from the handler.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    /// <returns>The response returned by the handler.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification to all configured handlers.
    /// </summary>
    /// <typeparam name="TNotification">Type of the notification.</typeparam>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
