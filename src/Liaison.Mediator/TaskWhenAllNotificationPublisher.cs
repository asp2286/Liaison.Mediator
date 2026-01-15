using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Publishes notifications by running all handlers concurrently and awaiting completion.
/// </summary>
public sealed class TaskWhenAllNotificationPublisher : INotificationPublisher
{
    /// <inheritdoc />
    public Task Publish(
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

        var tasks = new List<Task>();
        foreach (var executor in handlerExecutors)
        {
            tasks.Add(executor.HandlerCallback(notification, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }
}

