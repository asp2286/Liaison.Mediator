using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Handles a notification broadcast.
/// </summary>
/// <typeparam name="TNotification">Type of notification message.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    Task Handle(TNotification notification, CancellationToken cancellationToken = default);
}
