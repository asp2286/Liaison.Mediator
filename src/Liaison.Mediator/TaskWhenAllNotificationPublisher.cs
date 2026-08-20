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

        if (handlerExecutors is IReadOnlyCollection<NotificationHandlerExecutor> executorCollection)
        {
            var tasks = new Task[executorCollection.Count];
            var index = 0;
            foreach (var executor in executorCollection)
            {
                tasks[index++] = InvokeHandler(executor, notification, cancellationToken);
            }

            return Task.WhenAll(tasks);
        }

        var taskList = new List<Task>();
        foreach (var executor in handlerExecutors)
        {
            taskList.Add(InvokeHandler(executor, notification, cancellationToken));
        }

        return Task.WhenAll(taskList);
    }

    private static Task InvokeHandler(
        NotificationHandlerExecutor executor,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // A synchronous throw must not stop the remaining handlers from starting;
        // it has to surface through the aggregate task like any other failure.
        try
        {
            return executor.HandlerCallback(notification, cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}

