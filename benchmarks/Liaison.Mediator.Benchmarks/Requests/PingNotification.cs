namespace Liaison.Mediator.Benchmarks.Requests;

public sealed record PingNotification(int Value)
    : Liaison.Mediator.INotification,
      MediatR.INotification;
