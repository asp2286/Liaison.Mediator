namespace Liaison.Mediator;

/// <summary>
/// Factory methods that produce dispatch-table entries from compile-time-known
/// message types. Source-generated dispatch tables call these; hand-written
/// tables can use them the same way.
/// </summary>
/// <remarks>
/// Every entry closes its generics at compile time, so building and using a
/// table through these factories involves no <c>MakeGenericType</c>, no
/// <c>Activator</c>, and no trim- or AOT-unsafe code.
/// </remarks>
public static class MediatorDispatch
{
    /// <summary>
    /// Creates the dispatch entry for a request type whose response type is
    /// known at compile time. Requests implementing the plain
    /// <see cref="IRequest"/> use <see cref="Unit"/> as the response type.
    /// </summary>
    /// <typeparam name="TRequest">The concrete request type.</typeparam>
    /// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
    public static RequestDispatchEntry ForRequest<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        return new RequestDispatchEntry(
            typeof(TRequest),
            static serviceProvider => ServiceProviderMediator.CreateRequestWrapper<TRequest, TResponse>(serviceProvider));
    }

    /// <summary>
    /// Creates the dispatch entry for a notification type.
    /// </summary>
    /// <typeparam name="TNotification">The concrete notification type.</typeparam>
    public static NotificationDispatchEntry ForNotification<TNotification>()
        where TNotification : INotification
    {
        return new NotificationDispatchEntry(
            typeof(TNotification),
            static (serviceProvider, notificationPublisher) =>
                ServiceProviderMediator.CreateNotificationWrapper<TNotification>(serviceProvider, notificationPublisher));
    }
}
