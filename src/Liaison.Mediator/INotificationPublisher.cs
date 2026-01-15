using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Defines how notification handlers are invoked when a notification is published.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to the provided handler callbacks.
    /// </summary>
    /// <param name="handlerExecutors">Handler instances and their callbacks.</param>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents a notification handler instance and a callback used to invoke it.
/// </summary>
public readonly struct NotificationHandlerExecutor
{
    /// <summary>
    /// Creates a new executor.
    /// </summary>
    /// <param name="handlerInstance">The handler instance.</param>
    /// <param name="handlerCallback">Callback that invokes the handler.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlerInstance"/> or <paramref name="handlerCallback"/> is <see langword="null"/>.</exception>
    public NotificationHandlerExecutor(object handlerInstance, Func<INotification, CancellationToken, Task> handlerCallback)
    {
        HandlerInstance = handlerInstance ?? throw new ArgumentNullException(nameof(handlerInstance));
        HandlerCallback = handlerCallback ?? throw new ArgumentNullException(nameof(handlerCallback));
    }

    /// <summary>
    /// The handler instance.
    /// </summary>
    public object HandlerInstance { get; }

    /// <summary>
    /// Callback that invokes the handler.
    /// </summary>
    public Func<INotification, CancellationToken, Task> HandlerCallback { get; }
}

