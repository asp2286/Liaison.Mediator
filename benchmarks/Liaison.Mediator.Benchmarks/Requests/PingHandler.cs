namespace Liaison.Mediator.Benchmarks.Requests;

public sealed class PingHandler
    : Liaison.Mediator.IRequestHandler<Ping, Pong>,
      MediatR.IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Pong(request.Value + 1));
    }
}
