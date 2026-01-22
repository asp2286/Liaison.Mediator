namespace Liaison.Mediator.Benchmarks.Requests;

public sealed class PingNotificationHandler
    : Liaison.Mediator.INotificationHandler<PingNotification>,
      MediatR.INotificationHandler<PingNotification>
{
    public Task Handle(PingNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
