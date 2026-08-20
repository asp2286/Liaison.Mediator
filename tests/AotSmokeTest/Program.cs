using Liaison.Mediator;

// Exercises the builder-mode surface under Native AOT: one request/response
// round trip and one notification fanned out to two handlers. Any IL warning
// fails the publish (TreatWarningsAsErrors); any behavioral mismatch exits
// non-zero so CI fails the run.
var builder = new MediatorBuilder();
var firstObserver = new CountingHandler();
var secondObserver = new CountingHandler();

builder.RegisterRequestHandler<Ping, Pong>(new PingHandler())
       .RegisterNotificationHandler<Pinged>(firstObserver)
       .RegisterNotificationHandler<Pinged>(secondObserver);

IMediator mediator = builder.Build();

Pong pong = await mediator.Send(new Ping("smoke"));
if (pong.Message != "smoke-pong")
{
    Console.Error.WriteLine($"Send returned '{pong.Message}' instead of 'smoke-pong'.");
    return 1;
}

await mediator.Publish(new Pinged("smoke"));
if (firstObserver.Count != 1 || secondObserver.Count != 1)
{
    Console.Error.WriteLine($"Notification handlers ran {firstObserver.Count} and {secondObserver.Count} times; expected 1 and 1.");
    return 1;
}

Console.WriteLine("AOT smoke test passed.");
return 0;

public sealed record Ping(string Message) : IRequest<Pong>;

public sealed record Pong(string Message);

public sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Pong($"{request.Message}-pong"));
    }
}

public sealed record Pinged(string Message) : INotification;

public sealed class CountingHandler : INotificationHandler<Pinged>
{
    public int Count { get; private set; }

    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        Count++;

        return Task.CompletedTask;
    }
}
