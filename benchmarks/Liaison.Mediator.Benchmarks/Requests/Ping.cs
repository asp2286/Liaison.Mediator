namespace Liaison.Mediator.Benchmarks.Requests;

public sealed record Ping(int Value)
    : Liaison.Mediator.IRequest<Pong>,
      MediatR.IRequest<Pong>;

public sealed record Pong(int Value);
