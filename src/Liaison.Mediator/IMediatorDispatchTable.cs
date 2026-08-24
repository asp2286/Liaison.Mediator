using System;

namespace Liaison.Mediator;

/// <summary>
/// Maps concrete message types to pre-built dispatch entries so the
/// dependency-injection mediator can create its dispatch wrappers without
/// runtime reflection.
/// </summary>
/// <remarks>
/// Lookups happen on the first dispatch of each message type per mediator
/// instance and may run concurrently; implementations must be safe for
/// concurrent reads and should return the same entry for the same type on
/// every call. A lookup miss is final: the mediator throws
/// <see cref="InvalidOperationException"/> and never falls back to
/// reflection-based dispatch.
/// </remarks>
public interface IMediatorDispatchTable
{
    /// <summary>
    /// Finds the dispatch entry for a concrete request type.
    /// </summary>
    /// <param name="requestType">The concrete request type to look up.</param>
    /// <returns>The entry, or <see langword="null"/> when the type has no entry.</returns>
    RequestDispatchEntry? FindRequestEntry(Type requestType);

    /// <summary>
    /// Finds the dispatch entry for a concrete notification type.
    /// </summary>
    /// <param name="notificationType">The concrete notification type to look up.</param>
    /// <returns>The entry, or <see langword="null"/> when the type has no entry.</returns>
    NotificationDispatchEntry? FindNotificationEntry(Type notificationType);
}

/// <summary>
/// An immutable dispatch-table entry for one concrete request type. Create
/// entries with <see cref="MediatorDispatch.ForRequest{TRequest, TResponse}()"/>.
/// </summary>
/// <remarks>
/// Entries are immutable and safe to share across threads, scopes, and
/// containers. The wrapped factory may be invoked more than once (once per
/// mediator instance, or several times under a cold-dispatch race).
/// </remarks>
public sealed class RequestDispatchEntry
{
    private readonly Func<IServiceProvider, IRequestHandlerWrapper> _createWrapper;

    internal RequestDispatchEntry(Type requestType, Func<IServiceProvider, IRequestHandlerWrapper> createWrapper)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        _createWrapper = createWrapper ?? throw new ArgumentNullException(nameof(createWrapper));
    }

    /// <summary>
    /// The concrete request type this entry dispatches.
    /// </summary>
    public Type RequestType { get; }

    internal IRequestHandlerWrapper CreateWrapper(IServiceProvider serviceProvider)
        => _createWrapper(serviceProvider);
}

/// <summary>
/// An immutable dispatch-table entry for one concrete notification type. Create
/// entries with <see cref="MediatorDispatch.ForNotification{TNotification}()"/>.
/// </summary>
/// <remarks>
/// Entries are immutable and safe to share across threads, scopes, and
/// containers. The wrapped factory may be invoked more than once (once per
/// mediator instance, or several times under a cold-dispatch race).
/// </remarks>
public sealed class NotificationDispatchEntry
{
    private readonly Func<IServiceProvider, INotificationPublisher, INotificationHandlerWrapper> _createWrapper;

    internal NotificationDispatchEntry(
        Type notificationType,
        Func<IServiceProvider, INotificationPublisher, INotificationHandlerWrapper> createWrapper)
    {
        NotificationType = notificationType ?? throw new ArgumentNullException(nameof(notificationType));
        _createWrapper = createWrapper ?? throw new ArgumentNullException(nameof(createWrapper));
    }

    /// <summary>
    /// The concrete notification type this entry dispatches.
    /// </summary>
    public Type NotificationType { get; }

    internal INotificationHandlerWrapper CreateWrapper(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher)
        => _createWrapper(serviceProvider, notificationPublisher);
}
