namespace Liaison.Mediator.Benchmarks.Requests;

public sealed class PingPipeline
    : Liaison.Mediator.IPipelineBehavior<Ping, Pong>,
      MediatR.IPipelineBehavior<Ping, Pong>
{
    Task<Pong> Liaison.Mediator.IPipelineBehavior<Ping, Pong>.Handle(
        Ping request,
        Liaison.Mediator.RequestHandlerDelegate<Pong> next,
        CancellationToken cancellationToken)
    {
        return next();
    }

    Task<Pong> MediatR.IPipelineBehavior<Ping, Pong>.Handle(
        Ping request,
        MediatR.RequestHandlerDelegate<Pong> next,
        CancellationToken cancellationToken)
    {
        return next();
    }
}
