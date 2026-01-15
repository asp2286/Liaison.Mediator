using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Publishes notifications by awaiting each handler sequentially.
/// </summary>
public sealed class ForeachAwaitNotificationPublisher : INotificationPublisher
{
    /// <inheritdoc />
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        if (handlerExecutors is null)
        {
            throw new ArgumentNullException(nameof(handlerExecutors));
        }

        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        foreach (var executor in handlerExecutors)
        {
            await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

